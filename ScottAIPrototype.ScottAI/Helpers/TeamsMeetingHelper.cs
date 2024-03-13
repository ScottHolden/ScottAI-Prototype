namespace ScottAIPrototype;

public class TeamsMeetingHelper
{
	public static string ExtractThreadIdFromTeamsLink(string teamsMeetingUrl)
	{
		int startThreadId = teamsMeetingUrl.IndexOf("19:meeting_");
		if (startThreadId < 0)
		{
			startThreadId = teamsMeetingUrl.IndexOf("19%3ameeting_"); // URL encoded cases 
		}
		int endThreadId = teamsMeetingUrl.IndexOf("/", startThreadId);
		return System.Net.WebUtility.UrlDecode(teamsMeetingUrl.Substring(startThreadId, endThreadId - startThreadId));
	}
}