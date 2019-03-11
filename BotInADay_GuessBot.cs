// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BotInADay_Guess.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BotInADay_Guess
{
	public class BotInADay_GuessBot : IBot
	{
		private readonly UserState userState;
		private readonly ConversationState conversationState;
		private readonly IStatePropertyAccessor<GuessState> guessStateAccessor;
		private readonly IStatePropertyAccessor<DialogState> dialogStateAccessor;
		private readonly DialogSet dialogs;
		private readonly BotServices services;

		public BotInADay_GuessBot(BotServices services, UserState userState, ConversationState conversationState, ILoggerFactory loggerFactory)
		{
			this.userState = userState;
			this.conversationState = conversationState;
			this.services = services;
			guessStateAccessor = userState.CreateProperty<GuessState>(nameof(GuessState));
			dialogStateAccessor = conversationState.CreateProperty<DialogState>(nameof(DialogState));
			dialogs = new DialogSet(dialogStateAccessor);
			dialogs.Add(new GuessDialog(guessStateAccessor, loggerFactory));
		}

	public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
	{
		// Create a dialog context since we're using dialogs
		var dc = await dialogs.CreateContextAsync(turnContext, cancellationToken);

		if (turnContext.Activity.Type == ActivityTypes.Message)
		{
			var topIntent = await GetTopIntent(cancellationToken, turnContext);

			if ("Cancel".Equals(topIntent))
			{
				await dc.CancelAllDialogsAsync(cancellationToken);
				await turnContext.SendActivityAsync("Let's start over. You have my full attention. How can I help?", cancellationToken: cancellationToken);
			}

			// if there's a current dialog, let it have control and process the input
			var dialogResult = await dc.ContinueDialogAsync(cancellationToken);

			if (dialogResult.Status == DialogTurnStatus.Complete)
			{
				await WelcomeUser(cancellationToken, turnContext.Activity, dc);
			}

				// user sent us a message. 
				if (!dc.Context.Responded) // did we send a response yet?
			{
				// examine results from active dialog to decide how to act
				switch (dialogResult.Status)
				{
					case DialogTurnStatus.Empty:
						// there's nothing on the dialog stack
						// so we must feel responsible for the communication
						if ("Game" == topIntent || "guess" == (turnContext.Activity.Value as JObject)?.SelectToken("$..command")?.Value<string>())
						{
							await dc.BeginDialogAsync(nameof(GuessDialog), cancellationToken: cancellationToken);
						}
						else
						{
							await turnContext.SendActivityAsync($"You said '{turnContext.Activity.Text}'", cancellationToken: cancellationToken);
						}
						break;

					case DialogTurnStatus.Complete:
						await dc.EndDialogAsync(cancellationToken: cancellationToken);
						break;

					case DialogTurnStatus.Waiting:
						break;

					case DialogTurnStatus.Cancelled:
						break;

					default:
						await dc.CancelAllDialogsAsync(cancellationToken);
						break;
				}
			}
		}
		else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
		{
			if (turnContext.Activity.MembersAdded != null)
			{
				// Iterate over all new members added to the conversation.
				foreach (var member in turnContext.Activity.MembersAdded)
				{
					// Greet anyone that was not the target (recipient) of this message.
					if (member.Id != turnContext.Activity.Recipient.Id)
					{
						await WelcomeUser(cancellationToken, turnContext.Activity, dc);
					}
				}
			}
		}
		await conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
		await userState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
	}

		private async Task<string> GetTopIntent(CancellationToken cancellationToken, ITurnContext ctx)
		{
			if (string.IsNullOrEmpty(ctx.Activity.Text))
			{
				return null;
			}

			var luisResults = await services.Luis.RecognizeAsync(ctx, cancellationToken);
			var topScoringIntent = luisResults?.GetTopScoringIntent();
			return topScoringIntent?.intent;
		}

		private async Task WelcomeUser(CancellationToken cancellationToken, Activity activity, DialogContext dc)
		{
			var menuCard = CreateMenuAttachment();
			var response = activity.CreateReply();
			response.Attachments = new List<Attachment>() { menuCard };
			await dc.Context.SendActivityAsync(response, cancellationToken);
		}

		private Attachment CreateMenuAttachment()
		{
			var assembly = typeof(BotInADay_GuessBot).GetTypeInfo().Assembly;
			string adaptiveCard = null;
			using (var reader = new StreamReader(assembly.GetManifestResourceStream("BotInADay_Guess.Dialogs.MenuCard.json")))
			{
				adaptiveCard = reader.ReadToEnd();
			}

			return new Attachment()
			{
				ContentType = "application/vnd.microsoft.card.adaptive",
				Content = JsonConvert.DeserializeObject(adaptiveCard),
			};
		}
	}
}
