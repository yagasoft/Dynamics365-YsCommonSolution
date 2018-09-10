#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LinkDev.Libraries.Common;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace LinkDev.WfEngine.ManageSvc.Helpers
{
	internal class RoutingHelper
	{
		private readonly IOrganizationService service;
		private readonly CrmLog log;

		internal RoutingHelper(IOrganizationService service, CrmLog log)
		{
			this.service = service;
			this.log = log;
		}

		#region Routing

		internal EntityReference RouteToRole(EntityReference recordRef, Guid? roleId, Entity record,
			Guid? defaultRoutingUser = null, bool isPickRegarding = true, bool isLeastLoadedRouting = false)
		{
			try
			{
				log.LogFunctionStart();

				if (roleId == null)
				{
					throw new InvalidPluginExecutionException("Role in stage configuration is not set.");
				}

				var role = service.Retrieve(RoleConfiguration.EntityLogicalName, roleId.Value,
					new ColumnSet(RoleConfiguration.Fields.Type, RoleConfiguration.Fields.Queue,
						RoleConfiguration.Fields.Team, RoleConfiguration.Fields.User)).ToEntity<RoleConfiguration>();

				log.Log("Retrieved role.", LogLevel.Debug);

				if (role.Type == null)
				{
					throw new InvalidPluginExecutionException("Role type is not specified.");
				}

				log.LogLine();

				var owner = (EntityReference) record["ownerid"];

				switch (role.Type)
				{
					case RoleConfiguration.TypeEnum.Queue:
						if (role.Queue == null)
						{
							throw new InvalidPluginExecutionException("Queue role is not set in role configuration.");
						}

						log.LogLine();

						if (defaultRoutingUser.HasValue)
						{
							owner = new EntityReference(User.EntityLogicalName, defaultRoutingUser.Value);
							AssignRecord(recordRef, owner);
							log.Log($"Assigned record to default user: {defaultRoutingUser}");
						}

						AddToQueue(recordRef, role.Queue.Value);
						log.Log($"Assigned to queue: {role.QueueName}");

						// if 'pick record' is set in role config, then set it in task to assign the record to same user as task when picking
						if (recordRef.LogicalName == Task.EntityLogicalName && isPickRegarding)
						{
							service.Update(new Task
							               {
								               Id = recordRef.Id,
								               PickRecord = true
							               });
							log.Log("Set 'pick record' flag to 'true'.");
						}

						break;

					case RoleConfiguration.TypeEnum.Team:
						if (role.Team == null)
						{
							throw new InvalidPluginExecutionException("Team role is not set in role configuration.");
						}

						log.Log($"Record: {recordRef.Name}, ID: {recordRef.Id}"
						        + $". Team: {role.TeamName}" + (role.Team != null ? $", ID: {role.Team}" : ""));
						owner = new EntityReference(Team.EntityLogicalName, role.Team.Value);

						// check least loaded user
						if (isLeastLoadedRouting)
						{
							var userId = GetLeastLoadedUser(role.Team.Value, record.LogicalName);

							if (userId != null)
							{
								owner = new EntityReference(User.EntityLogicalName, userId.Value);
								log.Log("Assigned to least loaded user.");
							}
						}

						AssignRecord(recordRef, owner);
						log.Log($"Assigned to team: {role.TeamName}");
						break;

					case RoleConfiguration.TypeEnum.User:
						if (role.User == null)
						{
							throw new InvalidPluginExecutionException("User role is not set in role configuration.");
						}

						log.LogLine();
						owner = new EntityReference(User.EntityLogicalName, role.User.Value);
						AssignRecord(recordRef, owner);
						log.Log($"Assigned to user: {role.User}");
						break;
				}

				return owner;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		internal EntityReference RouteToFieldValue(EntityReference recordRef, Entity record, string fieldValue,
			Guid? defaultRoutingUser = null, bool isPickRegarding = true, bool isLeastLoadedRouting = false)
		{
			try
			{
				log.LogFunctionStart();

				EntityReference owner = null;

				if (string.IsNullOrEmpty(fieldValue))
				{
					log.Log("Record 'field value' routing is empty in routing record.", LogLevel.Warning);
					return owner;
				}

				var path = new Queue<string>();

				// extract path nodes
				foreach (Match match in Regex.Matches(fieldValue, "{.*?}"))
				{
					path.Enqueue(match.Value.Replace("{", "").Replace("}", ""));
				}

				if (path.Count <= 0)
				{
					log.Log("Poorly formatted 'field value' routing in routing record.", LogLevel.Warning);
					return owner;
				}

				var valueRecord = record;

				// drillthrough until the final field is reached
				while (path.Count > 1)
				{
					var pathNode = path.Dequeue();
					var valueRef = valueRecord.Attributes.FirstOrDefault(pair => pair.Key == pathNode).Value as EntityReference;

					if (valueRef == null)
					{
						log.Log($"'{pathNode}' field is not a lookup.", LogLevel.Warning);
						return owner;
					}

					valueRecord = service.Retrieve(valueRef.LogicalName, valueRef.Id, new ColumnSet(path.Peek()));
					log.Log($"Retrieved field '{valueRef.LogicalName}':'{valueRef.Id}':'{path.Peek()}.");
				}

				var ownerField = path.Peek();

				owner = valueRecord.Attributes.FirstOrDefault(pair => pair.Key == ownerField).Value as EntityReference;

				if (owner == null)
				{
					log.Log($"'{ownerField}' field is null, or not a lookup.", LogLevel.Warning);
					return null;
				}

				// routing to role
				if (owner.LogicalName == RoleConfiguration.EntityLogicalName)
				{
					owner = RouteToRole(recordRef, owner.Id, record, defaultRoutingUser, isPickRegarding,
						isLeastLoadedRouting);
				}
				else if (owner.LogicalName == Team.EntityLogicalName)
				{
					// check least loaded user
					if (isLeastLoadedRouting)
					{
						var userId = GetLeastLoadedUser(owner.Id, record.LogicalName);

						if (userId != null)
						{
							owner = new EntityReference(User.EntityLogicalName, userId.Value);
							AssignRecord(recordRef, owner);
							log.Log("Assigned to least loaded user.");
						}
					}
				}
				else
				{
					AssignRecord(recordRef, owner);
					log.Log($"Assigned to field value '{fieldValue}'.");
				}

				return owner;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		internal void AssignRecord(EntityReference recordRef, EntityReference ownerRef)
		{
			try
			{
				log.LogFunctionStart();

				service.Update(new Entity(recordRef.LogicalName)
				               {
					               Id = recordRef.Id,
					               ["ownerid"] = ownerRef
				               });
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		internal void AssignRecord(EntityReference recordRef, string ownerLogicalName, Guid ownerId)
		{
			try
			{
				log.LogFunctionStart();
				AssignRecord(recordRef, new EntityReference(ownerLogicalName, ownerId));
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		public void AddToQueue(EntityReference assignedRecord, Guid assignee)
		{
			try
			{
				log.LogFunctionStart();

				var routeRequest = new AddToQueueRequest
				                   {
					                   Target = new EntityReference(assignedRecord.LogicalName, assignedRecord.Id),
					                   DestinationQueueId = assignee
				                   };

				service.Execute(routeRequest);
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		internal Guid? GetLeastLoadedUser(Guid teamId, string recordName)
		{
			try
			{
				log.LogFunctionStart();

				log.Log($"Getting least loaded user => team: '{teamId}', records: '{recordName}'");

				var primaryIdName = Libraries.Common.CrmHelpers.GetEntityAttribute<string>(service, recordName,
					Libraries.Common.CrmHelpers.EntityAttribute.PrimaryIdAttribute);

				var query = new FetchExpression(
					@"<fetch>
  <entity name=""systemuser"">
    <attribute name=""systemuserid"" alias=""userId"" />
    <link-entity name=""teammembership"" from=""systemuserid"" to=""systemuserid"" intersect=""true"">
      <link-entity name=""team"" from=""teamid"" to=""teamid"" >
        <filter>
          <condition attribute=""teamid"" operator=""eq"" value=""" + teamId + @""" />
        </filter>
      </link-entity>
    </link-entity>
    <link-entity name=""" + recordName +
					@""" from=""owninguser"" to=""systemuserid"" link-type=""outer"" alias=""record"">
      <attribute name=""" + primaryIdName + @""" alias=""recordId"" />
    </link-entity>
  </entity>
</fetch>");

				var result = service.RetrieveMultiple(query);

				var leastLoadedUser = result.Entities
					.Select(resultQ => new
					                   {
						                   userId = (Guid) ((AliasedValue) resultQ["userId"]).Value,
						                   recordId = (Guid?)
						                              ((AliasedValue) resultQ.Attributes
							                                              .FirstOrDefault(pair => pair.Key == "recordId").Value)?.Value
					                   })
					.GroupBy(assignment => assignment.userId)
					.Select(group => new
					                 {
						                 userId = group.Key,
						                 count = group.Count(groupQ => groupQ.recordId != null)
					                 })
					.OrderBy(assignment => assignment.count)
					.FirstOrDefault();

				log.Log($"Least loaded user => '{leastLoadedUser?.userId}':'{leastLoadedUser?.count}'");

				return leastLoadedUser?.userId;
			}
			catch (Exception ex)
			{
				log.Log(ex);
				throw;
			}
			finally
			{
				log.LogFunctionEnd();
			}
		}

		#endregion
	}
}
