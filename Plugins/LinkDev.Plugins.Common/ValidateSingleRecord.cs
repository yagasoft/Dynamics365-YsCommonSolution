#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using LinkDev.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace LinkDev.Plugins.Common
{
	/// <summary>
	///     This plugin validates that there is only one record based on the field logical name passed in the config.<br />
	///     Must be registered in post-operation. For 'update' message, add a post image containing all fields checked.<br />
	///     Author: Ahmed el-Sawalhy<br />
	///     Version: 1.3.1
	/// </summary>
	public class ValidateSingleRecord : IPlugin
	{
		private readonly List<string> fieldsCompare = new List<string>();

		public ValidateSingleRecord(string unsecureConfig, string secureConfig)
		{
			if (!string.IsNullOrEmpty(unsecureConfig))
			{
				fieldsCompare = unsecureConfig.Split(',').ToList();
			}
		}

		public void Execute(IServiceProvider serviceProvider)
		{
			new ValidateSingleRecordLogic(fieldsCompare).Execute(this, serviceProvider);
		}
	}

	internal class ValidateSingleRecordLogic : PluginLogic<ValidateSingleRecord>
	{
		private readonly List<string> fieldsCompare;

		public ValidateSingleRecordLogic(List<string> fieldsCompare) : base(null, PluginStage.All)
		{
			this.fieldsCompare = fieldsCompare;
		}

		protected override void ExecuteLogic()
		{
			// get the triggering record
			var target = (Entity)context.InputParameters["Target"];

			Libraries.Common.CrmHelpers.LogAttributeValues(target.Attributes, target, log);

			tracingService.Trace("Getting the count of records ...");

			if (context.MessageName == "Update" && !context.PostEntityImages.Any())
			{
				throw new InvalidPluginExecutionException("'Update' message in uniqueness validator requires a full post image.");
			}

			// get all attributes in the entity and image
			tracingService.Trace("Parsing image ...");
			foreach (var attribute in context.PostEntityImages
				.SelectMany(entityImage => entityImage.Value.Attributes))
			{
				target[attribute.Key] = attribute.Value;
			}

			tracingService.Trace("Building query.");
			var query = new QueryExpression(target.LogicalName)
						{
							ColumnSet = new ColumnSet(false),
							NoLock = true
						};

			tracingService.Trace("Adding conditions ...");
			foreach (var field in fieldsCompare)
			{
				var fieldValue = target.GetAttributeValue<object>(field);

				// compare null value
				if (fieldValue == null)
				{
					tracingService.Trace($"Adding condition {field} == null.");
					query.Criteria.AddCondition(field, ConditionOperator.Null);
					continue;
				}

				tracingService.Trace($"Adding condition {field} == {fieldValue}.");

				// criteria accepts pure values only!!!
				if (fieldValue is OptionSetValue)
				{
					query.Criteria.AddCondition(field, ConditionOperator.Equal,
						((OptionSetValue)fieldValue).Value);
				}
				else if (fieldValue is EntityReference)
				{
					query.Criteria.AddCondition(field, ConditionOperator.Equal,
						((EntityReference)fieldValue).Id);
				}
				else
				{
					query.Criteria.AddCondition(field, ConditionOperator.Equal, fieldValue);
				}
			}

			var isActivity = target.Contains("activityid");

			query.Criteria.AddCondition(isActivity ? "activityid" : context.PrimaryEntityName + "id",
				ConditionOperator.NotEqual, context.PrimaryEntityId);

			tracingService.Trace("Executing query and getting count.");
			var count = service.RetrieveMultiple(query).Entities.Count;

			// throw an exception if there is more than one record with the same value in this field
			if (count > 0)
			{
				throw new InvalidPluginExecutionException("Can't have more than one record with the same value in field[s] "
					+ fieldsCompare.Aggregate((field1, field2) => "'" + field1 + "', '" + field2 + "'") + ".");
			}

			tracingService.Trace("DONE!");
		}

		protected override bool IsContextValid()
		{
			return context.MessageName == "Create" || context.MessageName == "Update";
		}
	}

}
