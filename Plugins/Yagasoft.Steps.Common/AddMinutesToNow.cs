//         Project / File: PluginProject1 / WFName1.cs

#region Imports

using System;
using System.Activities;
using Yagasoft.Libraries.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;

#endregion

namespace Yagasoft.Steps.Common
{
	public class AddMinutesToNow : CodeActivity
	{
		#region Arguments

		[RequiredArgument]
		[Input("Minutes to Add")]
		[Default("0")]
		public InArgument<int> MinutesArg { get; set; }

		[RequiredArgument]
		[Output("New Date")]
		public OutArgument<DateTime> NewDateArg { get; set; }

		#endregion

		protected override void Execute(CodeActivityContext context)
		{
			new AddMinutesToNowLogic().Execute(this, context, false);
		}
	}

	public class AddMinutesToNowLogic : StepLogic<AddMinutesToNow>
	{
		protected override void ExecuteLogic()
		{
			var minutes = codeActivity.MinutesArg.Get<int>(ExecutionContext);

			var newDate = DateTime.UtcNow.AddMinutes(minutes);
			log.Log($"New date: '{newDate}'.");
			log.SetTitle($"New date: '{newDate}' for \"{{name}}\".");

			codeActivity.NewDateArg.Set(ExecutionContext, newDate);
		}
	}
}
