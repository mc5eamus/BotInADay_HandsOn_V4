using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Configuration;

namespace BotInADay_Guess
{
	public class BotServices
	{
		public LuisRecognizer Luis { get; }

		public BotServices(BotConfiguration config)
		{
			var luis = config.Services.Find(_ => ServiceTypes.Luis.Equals(_.Type)) as LuisService;
			var app = new LuisApplication(luis.AppId, luis.AuthoringKey, luis.GetEndpoint());
			Luis = new LuisRecognizer(app);
		}
	}
}

