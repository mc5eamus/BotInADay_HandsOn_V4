namespace BotInADay_Guess.Dialogs
{
	public class GuessState
	{
		// Random number to guess
		public int Number { get; set; }
		// Turns user tooks to guess so far
		public int Counter { get; set; }
		// personal record
		public int BestScore { get; set; }
	}
}
