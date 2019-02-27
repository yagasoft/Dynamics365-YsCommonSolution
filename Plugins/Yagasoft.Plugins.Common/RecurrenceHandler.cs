//         Project / File: Yagasoft.Plugins.Common / CustomJobHandler.cs

#region Imports

using System;
using System.Linq;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using static Yagasoft.Libraries.Common.CrmHelpers;
using static Yagasoft.Libraries.Common.MetadataHelpers;

#endregion

namespace Yagasoft.Plugins.Common
{
	public class RecurrenceHandler : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			new RecurrenceHandlerLogic().Execute(this, serviceProvider);
		}
	}

	internal class RecurrenceHandlerLogic : PluginLogic<RecurrenceHandler>
	{
		public RecurrenceHandlerLogic() : base(null, PluginStage.All)
		{ }

		protected override void ExecuteLogic()
		{
			var exclusions = new[]
							 {
									 RecurrenceRuleException.EntityLogicalName,
									 RecurrenceRuleExceptionGrouping.EntityLogicalName
								 };

			if (context.MessageName == "Associate" || context.MessageName == "Disassociate")
			{
				log.Log($"Message: '{context.MessageName}'.");

				// Get the "Relationship" Key from context
				if (context.InputParameters.Contains("Relationship"))
				{
					var relationName = context.InputParameters["Relationship"].ToString();
					log.Log($"Relation name: '{relationName}'.");
				}
				else
				{
					log.Log("Not a relationship.", LogLevel.Warning);
					return;
				}

				EntityReference[] related;

				// Get the "Relationship" Key from context
				if (context.InputParameters.Contains("RelatedEntities"))
				{
					related = ((EntityReferenceCollection)context.InputParameters["RelatedEntities"])?.ToArray();
					log.Log($"Related records: '{related?.Length}'.");

					if (related == null)
					{
						log.Log("No related entities.", LogLevel.Warning);
						return;
					}
					else
					{
						related = related.Where(record => !exclusions.Contains(record.LogicalName)).ToArray();

						foreach (var relatedRecord in related)
						{
							log.Log($"Related: '{relatedRecord.LogicalName}':'{relatedRecord.Id}'.");
						}
					}
				}
				else
				{
					log.Log("No related entities.", LogLevel.Warning);
					return;
				}

				var targetRef = (EntityReference)context.InputParameters["Target"];
				log.Log($"Target: '{targetRef?.LogicalName}':'{targetRef?.Id}'.");

				if (targetRef == null)
				{
					log.Log("No target.", LogLevel.Warning);
					return;
				}

				if (targetRef.LogicalName == RecurrenceRule.EntityLogicalName)
				{
					foreach (var relatedRecord in related)
					{
						log.Log($"Triggering update of record: '{relatedRecord.LogicalName}':'{relatedRecord.Id}'.");
						service.Update(new Entity(relatedRecord.LogicalName)
						{
							Id = relatedRecord.Id,
							["ldv_recurrenceupdatedtrigger"] = DateTime.Now.ToString()
						});
					}
				}
				else if (related.Any(record => record.LogicalName == RecurrenceRule.EntityLogicalName)
						 && !exclusions.Contains(targetRef.LogicalName))
				{
					log.Log($"Triggering update of record: '{targetRef.LogicalName}':'{targetRef.Id}'.");
					service.Update(new Entity(targetRef.LogicalName)
					{
						Id = targetRef.Id,
						["ldv_recurrenceupdatedtrigger"] = DateTime.Now.ToString()
					});
				}
			}
			else if (context.MessageName == "Update")
			{
				var recurrence = context.PostEntityImages.FirstOrDefault().Value?.ToEntity<RecurrenceRule>();
				log.Log($"Target: '{recurrence?.LogicalName}':'{recurrence?.Id}'.");

				if (recurrence == null)
				{
					throw new InvalidPluginExecutionException("Can't find a full post-image registered for this step.");
				}

				var records = GetRelatedRecords(service, recurrence.ToEntityReference(),
					new[] { RelationType.OneToManyRelationships, RelationType.ManyToManyRelationships },
					context.OrganizationId.ToString())
					.Where(record => !exclusions.Contains(record.LogicalName)).Distinct(new EntityComparer());

				foreach (var record in records)
				{
					var recordTemp = record;
					log.Log($"Record: '{recordTemp.LogicalName}':'{recordTemp.Id}'.");

					service.Update(new Entity(recordTemp.LogicalName)
					{
						Id = recordTemp.Id,
						["ldv_recurrenceupdatedtrigger"] = DateTime.Now.ToString()
					});
				}
			}
			else
			{
				throw new InvalidPluginExecutionException($"Plugin registered on wrong message '{context.MessageName}'.");
			}
		}
	}
}
