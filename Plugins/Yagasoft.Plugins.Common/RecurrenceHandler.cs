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
			var exclusions =
				new[]
							 {
									 RecurrenceRuleException.EntityLogicalName,
									 RecurrenceRuleExceptionGrouping.EntityLogicalName
								 };

			if (Context.MessageName == "Update")
			{
				var recurrence = Context.PostEntityImages.FirstOrDefault().Value?.ToEntity<RecurrenceRule>();
				log.Log($"Target: '{recurrence?.LogicalName}':'{recurrence?.Id}'.");

				if (recurrence == null)
				{
					throw new InvalidPluginExecutionException("Can't find a full post-image registered for this step.");
				}

				var records = GetRelatedRecords(Service, recurrence.ToEntityReference(),
[ RelationType.OneToManyRelationships, RelationType.ManyToManyRelationships ],
					orgId:Context.OrganizationId)
					.Where(record => !exclusions.Contains(record.LogicalName)).Distinct(new EntityComparer());

				foreach (var record in records)
				{
					var recordTemp = record;
					log.Log($"Record: '{recordTemp.LogicalName}':'{recordTemp.Id}'.");

					Service.Update(
						new Entity(recordTemp.LogicalName)
					{
						Id = recordTemp.Id,
						["ys_recurrenceupdatedtrigger"] = DateTime.Now.ToString()
					});
				}
			}
			else
			{
				throw new InvalidPluginExecutionException($"Plugin registered on wrong message '{Context.MessageName}'.");
			}
		}
	}
}
