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
	public class GenerateRandomYagasoft : CodeActivity
	{
		#region Arguments

		[Input("Min Integer Value")]
		[Default("1")]
		public InArgument<int> MinIntArg { get; set; }

		[Input("Max Integer Value")]
		[Default("999999999")]
		public InArgument<int> MaxIntArg { get; set; }

		[RequiredArgument]
		[Input("Return GUID")]
		[Default("False")]
		public InArgument<bool> IsGuidArg { get; set; }

		[Output("Generated Integer")]
		public OutArgument<int> IntArg { get; set; }

		[Output("Generated String")]
		public OutArgument<string> StringArg { get; set; }

		#endregion

		protected override void Execute(CodeActivityContext context)
		{
			new GenerateRandomYagasoftLogic().Execute(this, context, false);
		}
	}

	internal class GenerateRandomYagasoftLogic : StepLogic<GenerateRandomYagasoft>
	{
		protected override void ExecuteLogic()
		{
			// get the triggering record
			var target = (Entity) context.InputParameters["Target"];

			var minInt = codeActivity.MinIntArg.Get<int>(executionContext);
			var maxInt = codeActivity.MaxIntArg.Get<int>(executionContext);
			var isGuid = codeActivity.IsGuidArg.Get<bool>(executionContext);

			var generatedInt = isGuid ? 0 : new System.Random().Next(minInt, maxInt + 1);
			var generatedString = isGuid ? Guid.NewGuid().ToString() : generatedInt.ToString();
			log.Log($"Generated '{generatedString}'.");

			codeActivity.IntArg.Set(executionContext, generatedInt);
			codeActivity.StringArg.Set(executionContext, generatedString);

			log.SetTitle(target, null, $"Generated '{generatedString}' for \"{{name}}\".");
		}
	}
}
