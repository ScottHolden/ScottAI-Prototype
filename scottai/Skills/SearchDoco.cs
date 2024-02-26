using System.Net.Http.Json;

namespace VoiceChat;

public class SearchDoco : ISkill
{
	private readonly HttpClient _httpClient = new();

	public string Name => "LINKSEARCH";

	public string Description => "followed by a search query when the user asks you to search for a link in Azure or Microsoft Documentation.";

	public async Task Invoke(string input, Func<string, Task> speak, Func<string, Task> chat)
	{
		await speak(string.Empty);
		var response = await _httpClient.GetFromJsonAsync<SearchResults>($"https://learn.microsoft.com/api/search?search={Uri.EscapeDataString(input)}&locale=en-us&%24top=1&expandScope=true&partnerId=LearnSite");
		var item = response?.results?.FirstOrDefault();
		if (item == null) return;
		await chat($@"<p><a href=""{item.url}""><b>{item.title}</b></a></p><p>{item.description}</p>");
	}

	private record SearchResults(SearchResultItem[] results);
	private record SearchResultItem(string title, string url, string description);
}