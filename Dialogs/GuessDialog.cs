using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace BotInADay_Guess.Dialogs
{
	public class GuessDialog : ComponentDialog
	{
		private const string NumberPrompt = "numberPrompt";
		private const string GuessFlow = "guessFlow";
		private readonly IStatePropertyAccessor<GuessState> guessStateAccessor;
		private readonly ILogger log;

		public GuessDialog(
		   IStatePropertyAccessor<GuessState> guessStateAccessor,
		   ILoggerFactory loggerFactory) : base(nameof(GuessDialog))
		{
			this.guessStateAccessor = guessStateAccessor;
			log = loggerFactory.CreateLogger<GuessDialog>();
			// Our dialog will have a few steps in a sequence
			var waterfallSteps = new WaterfallStep[]
			{
				InitializeStateStepAsync, // come up with a number and inform user
				PromptForNumberStepAsync, // ask user for her guess and validate the response
				EvaluateResultStepAsync // summarize the outcome
			};
			AddDialog(new WaterfallDialog(GuessFlow, waterfallSteps));
			// Alongside with the waterfall, we will be utilizing a NumberPrompt,
			// so lets register it here.
			// We'll put ValidateGuess method in charge of evaluating the user's response 
			AddDialog(new NumberPrompt<int>(NumberPrompt, ValidateGuess));
		}

		private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var rnd = new Random();
			// we're getting the stored value of the GuessState
			var state = await guessStateAccessor.GetAsync(
				stepContext.Context,
				() => new GuessState(), // default value simply provide a new instance
				cancellationToken);
			state.Number = rnd.Next(1, 100);
			state.Counter = 0;
			log.LogInformation($"New round. Number is {state.Number}");
			await guessStateAccessor.SetAsync(stepContext.Context,
				state,
				cancellationToken);
			await stepContext.Context.SendActivityAsync("I have a number between 1 and 99 in mind.", cancellationToken: cancellationToken);
			// NextAsync advances to the next step in the waterfall
			return await stepContext.NextAsync(cancellationToken: cancellationToken);
		}

		private async Task<DialogTurnResult> PromptForNumberStepAsync(
			WaterfallStepContext stepContext,
			CancellationToken cancellationToken)
		{
			var opts = new PromptOptions
			{
				Prompt = new Activity
				{
					Type = ActivityTypes.Message,
					Text = "What's your guess?",
				},
			};
			// we'll remaing in the NumberPromt component until the right number is provided
			return await stepContext.PromptAsync(NumberPrompt,
				opts,
				cancellationToken);
		}

		private async Task<DialogTurnResult> EvaluateResultStepAsync(
			WaterfallStepContext stepContext,
			CancellationToken cancellationToken)
		{
			var newBestScore = ".";
			var state = await guessStateAccessor.GetAsync(stepContext.Context, cancellationToken: cancellationToken);
			var counter = state.Counter;
			if (state.BestScore == 0 || state.Counter < state.BestScore)
			{
				if (state.BestScore != 0)
				{
					newBestScore = $", it's {state.BestScore - state.Counter} better than your previous best score!";
				}
				state.BestScore = state.Counter;
			}
			state.Counter = 0;
			await guessStateAccessor.SetAsync(stepContext.Context,
				state,
				cancellationToken);

			await stepContext.Context.SendActivityAsync($"Exactly! It took you {counter} turns{newBestScore}", cancellationToken: cancellationToken);
			// Fall out of the dialog
			return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
		}

		private async Task<bool> ValidateGuess(PromptValidatorContext<int> promptContext,
			CancellationToken cancellationToken)
		{
			// In the validator, we simply return true or false
			// depending on whether we're happy with the user input.
			// We can still provide feedback using SendActivity.
			var state = await this.guessStateAccessor.GetAsync(promptContext.Context, cancellationToken: cancellationToken);
			state.Counter++;
			await guessStateAccessor.SetAsync(promptContext.Context,
				state,
				cancellationToken);
			var value = promptContext.Recognized.Value;
			if (value <= 0 || value >= 100)
			{
				await promptContext.Context.SendActivityAsync("I'm pretty certain it's a number between 1 and 99. Give it another try!", cancellationToken: cancellationToken);
				return false;
			}

			if (value < state.Number || value > state.Number)
			{
				var hint = value < state.Number ? "bigger" : "smaller";
				await promptContext.Context.SendActivityAsync($"My number is {hint}, try again!", cancellationToken: cancellationToken);
				return false;
			}
			return true;
		}
	}
}
