#region Imports

using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Yagasoft.Libraries.Common;

#endregion

namespace Yagasoft.Plugins.Recurrence.Recalc
{
	public class PostOpRecurrenceCreateRecalc : IPlugin
	{
		private string unsecureConfig;

		public PostOpRecurrenceCreateRecalc(string unsecureConfig)
		{
			unsecureConfig.RequireFilled(nameof(unsecureConfig));
			this.unsecureConfig = unsecureConfig;
		}

		public void Execute(IServiceProvider serviceProvider)
		{
			new PostOpRecurrenceCreateRecalcLogic(unsecureConfig).Execute(this, serviceProvider);
		}
	}

	internal class PostOpRecurrenceCreateRecalcLogic : PluginLogic<PostOpRecurrenceCreateRecalc>
	{
		private string unsecureConfig;

		public PostOpRecurrenceCreateRecalcLogic(string unsecureConfig)
			: base("Create", PluginStage.PostOperation)
		{
			this.unsecureConfig = unsecureConfig;
		}

		protected override void ExecuteLogic()
		{
			unsecureConfig.RequireFilled(nameof(unsecureConfig));

			Log.Log($"Unsecure config: {unsecureConfig}");

			var split = unsecureConfig.Split(',');

			if (split.Length < 2)
			{
				throw new InvalidPluginExecutionException("Missing parameters in unsecure config.");
			}

			var parentLookup = split[0];
			var recurrenceTrigger = split[1];

			var parent = Target.GetAttributeValue<EntityReference>(parentLookup);

			parent.Require(nameof(parent));

			Log.Log($"Triggering update of record: {parent.LogicalName}:{parent.Id} ...");

			Service.Update(
				new Entity(parent.LogicalName)
				{
					Id = parent.Id,
					[recurrenceTrigger] = DateTime.Now.ToString()
				});
		}
	}
}
