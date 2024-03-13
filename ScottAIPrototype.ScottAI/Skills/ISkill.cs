namespace VoiceChat
{
	// This skill implementation was build prior to Azure OpenAI Functions
	// TODO: Refactor to Functions
	internal interface ISkill
	{
		public string Name { get; }
		public string Description { get; }
		public string Trigger => $"[{Name}]";
		public Task Invoke(string input, Func<string, Task> speakAsync, Func<string, Task> chatAsync);
	}
}
