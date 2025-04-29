using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Content.Client.UserInterface.Systems.Chat
{
public class ChatSlurFilterSystem
{
    private static readonly Dictionary<string, string> SlurDict = new Dictionary<string, string>(){
        {"(n|и)igge(r|я)","neighbor"},
        {"(n|и)igg(a|д)","ninja"},
        {"f(a|д)ggo(t|т)","fairy"},
        {"f(a|д)g","pixie"},
        {"(k|к)i(k|к)e","lawyer"},
        {"(t|т)(r|я)(a|д)(n|и)(n|и)(y|у)","vampire"},
        {"(t|т)(r|я)oo(n|и)","vampire"},
        {"coo(n|и)","woodpile-hider"}
    };
    public static string FilterSlurs(string message)
    {
        foreach (KeyValuePair<string, string> ele in SlurDict)
        {
            var Slur = ele.Key;
            var SlurReplacement = ele.Value;
            // Using the same system as ReplacementAccentSystem.cs, performance hit irrelevant due to this running clientside.
            var maskMessage = message;
            // Niggertoolbox wont let me use regex.count because MUH SANDBOX VIOLATION, so i instead use the even MORE malware-capable regex.matches :)
            for (int i = Regex.Matches(message, $"{Slur}", RegexOptions.IgnoreCase).Count; i > 0; i--)
            {
                // fetch the match again as the character indices may have changed
                Match match = Regex.Match(maskMessage, $"{Slur}", RegexOptions.IgnoreCase);
                var TempReplacement = SlurReplacement;

                // Intelligently replace capitalization
                // two cases where we will do so:
                // - the string is all upper case (just uppercase the replacement too)
                // - the first letter of the word is capitalized (common, just uppercase the first letter too)
                // any other cases are not really useful or not viable, since the match & replacement can be different
                // lengths

                // second expression here is weird--its specifically for single-word capitalization for I or A
                // dwarf expands I -> Ah, without that it would transform I -> AH
                // so that second case will only fully-uppercase if the replacement length is also 1
                if (!match.Value.Any(char.IsLower) && (match.Length > 1 || TempReplacement.Length == 1))
                {
                    TempReplacement = TempReplacement.ToUpperInvariant();
                }
                else if (match.Length >= 1 && TempReplacement.Length >= 1 && char.IsUpper(match.Value[0]))
                {
                    TempReplacement = TempReplacement[0].ToString().ToUpper() + TempReplacement[1..];
                }

                // In-place replace the match with the transformed capitalization replacement
                message = message.Remove(match.Index, match.Length).Insert(match.Index, TempReplacement);
                var mask = new string('_', TempReplacement.Length);
                maskMessage = maskMessage.Remove(match.Index, match.Length).Insert(match.Index, mask);
            }
        }

        return message;
    }
}
}
