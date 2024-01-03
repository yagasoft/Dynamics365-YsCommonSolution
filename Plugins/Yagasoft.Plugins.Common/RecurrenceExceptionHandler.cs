#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using static Yagasoft.Libraries.Common.CrmHelpers;

#endregion

namespace Yagasoft.Plugins.Common
{

	public class RecurrenceExceptionHandler : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			new RecurrenceExceptionHandlerLogic().Execute(this, serviceProvider);
		}
	}

	internal class RecurrenceExceptionHandlerLogic : PluginLogic<RecurrenceExceptionHandler>
	{
		public RecurrenceExceptionHandlerLogic() : base(null, PluginStage.All)
		{
		}

		protected override void ExecuteLogic()
		{
			var inclusions =
				new[]
			                 {
				                 RecurrenceRuleException.EntityLogicalName,
				                 RecurrenceRuleExceptionGrouping.EntityLogicalName
			                 };

			var rules = new List<RecurrenceRule>();

			if (context.MessageName == "Update")
			{
				var target = (Entity) context.InputParameters["Target"];
				log.Log($"Target: '{target?.LogicalName}':'{target?.Id}'.");

				if (target == null || !inclusions.Contains(target.LogicalName ?? ""))
				{
					throw new InvalidPluginExecutionException($"Plugin registered on wrong entity '{target?.LogicalName}'.");
				}

				LoadRules(target.ToEntityReference(), rules);
			}
			else
			{
				throw new InvalidPluginExecutionException($"Plugin registered on wrong message '{context.MessageName}'.");
			}

			log.Log($"Rules count: {rules.Count}.");

			foreach (var rule in rules.Distinct<RecurrenceRule>(new EntityComparer()).ToList())
			{
				log.Log($"Triggering update of rule: '{rule.LogicalName}':'{rule.Id}'.");
				rule.ExceptionUpdatedTrigger = DateTime.Now.ToString();
				service.Update(rule);
			}
		}

		private void LoadRules(EntityReference exclusionRecord, List<RecurrenceRule> rules)
		{
			try
			{
				log.LogFunctionStart();

				if (exclusionRecord.LogicalName == RecurrenceRuleExceptionGrouping.EntityLogicalName)
				{
					var record =
						new RecurrenceRuleExceptionGrouping
					             {
						             Id = exclusionRecord.Id
					             };
					record.LoadRelation(RecurrenceRuleExceptionGrouping.RelationNames.Rules, service);

					if (record.Rules != null)
					{
						rules.AddRange(record.Rules);
					}

					record.LoadRelation(RecurrenceRuleExceptionGrouping.RelationNames.Exceptions, service);

					if (record.Exceptions != null)
					{
						foreach (var exception in record.Exceptions)
						{
							exception.LoadRelation(RecurrenceRuleException.RelationNames.Rules, service);

							if (exception.Rules != null)
							{
								rules.AddRange(exception.Rules);
							}
						}
					}
				}
				else if (exclusionRecord.LogicalName == RecurrenceRuleException.EntityLogicalName)
				{
					var record =
						new RecurrenceRuleException
					             {
						             Id = exclusionRecord.Id
					             };

					record.LoadRelation(RecurrenceRuleException.RelationNames.Rules, service);

					if (record.Rules != null)
					{
						rules.AddRange(record.Rules);
					}

					record.LoadRelation(RecurrenceRuleException.RelationNames.ExceptionGroups, service);

					if (record.ExceptionGroups != null)
					{
						foreach (var group in record.ExceptionGroups)
						{
							group.LoadRelation(RecurrenceRuleExceptionGrouping.RelationNames.Rules, service);

							if (group.Rules != null)
							{
								rules.AddRange(group.Rules);
							}
						}
					}
				}
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
	}
}
