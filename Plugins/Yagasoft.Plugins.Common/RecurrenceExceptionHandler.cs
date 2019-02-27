//         Project / File: Yagasoft.Plugins.Common / CustomJobHandler.cs

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
			var inclusions = new[]
			                 {
				                 RecurrenceRuleException.EntityLogicalName,
				                 RecurrenceRuleExceptionGrouping.EntityLogicalName
			                 };

			var rules = new List<RecurrenceRule>();

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
					related = ((EntityReferenceCollection) context.InputParameters["RelatedEntities"])?.ToArray();
					log.Log($"Related records: '{related?.Length}'.");

					if (related == null)
					{
						log.Log("No related entities.", LogLevel.Warning);
						return;
					}
					else
					{
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

				var targetRef = (EntityReference) context.InputParameters["Target"];
				log.Log($"Target: '{targetRef?.LogicalName}':'{targetRef?.Id}'.");

				if (targetRef == null)
				{
					log.Log("No target.", LogLevel.Warning);
					return;
				}

				if (targetRef.LogicalName == RecurrenceRule.EntityLogicalName)
				{
					rules.Add(new RecurrenceRule { Id = targetRef.Id });
				}
				else if (related.Any(record => record.LogicalName == RecurrenceRule.EntityLogicalName)
				         && inclusions.Contains(targetRef.LogicalName))
				{
					rules.AddRange(related
						.Where(relatedRecord => relatedRecord.LogicalName == RecurrenceRule.EntityLogicalName)
						.Select(relatedRecord => new RecurrenceRule {Id = relatedRecord.Id}));
				}
				else if (related.All(record => inclusions.Contains(record.LogicalName))
				         && inclusions.Contains(targetRef.LogicalName))
				{
					LoadRules(targetRef, rules);

					foreach (var relatedRecord in related)
					{
						LoadRules(relatedRecord, rules);
					}
				}
			}
			else if (context.MessageName == "Update")
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
					var record = new RecurrenceRuleExceptionGrouping
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
					var record = new RecurrenceRuleException
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
