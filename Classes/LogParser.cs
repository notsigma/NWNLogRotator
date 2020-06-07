﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NWNLogRotator.Classes
{
    class LogParser
    {
        public string ParseLog(Stream inputStream, bool removeCombat, bool removeEvents, string ServerName, string ServerNameColor, string CustomEmotes, string FilterLines, bool ServerMode)
        {
            var reader = new StreamReader(inputStream, Encoding.GetEncoding("iso-8859-1"));
            var removeExps = new List<Regex>();

            string[] filterLinesArray = FilterLines.Split(',');
            if (!removeEvents && FilterLines != "")
            {
                eventLines = new List<String>();
                foreach (string eventString in filterLinesArray)
                {
                    string theEventString = eventString.Trim();
                    eventLines.Add(theEventString);
                }
                removeEvents = true;
            }
            else if (removeEvents && FilterLines != "")
            {
                List<String> eventLinesTemp = new List<String>();
                foreach (string theEvent in filterLinesArray)
                {
                    eventLinesTemp.Add(theEvent);
                }
                eventLines.AddRange(eventLinesTemp);
            }

            string text;
            if (removeEvents)
            {
                StringBuilder cleanTextBuilder = new StringBuilder();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!eventLines.Any(x => line.Contains(x)))
                        cleanTextBuilder.AppendLine(line);
                }
                text = cleanTextBuilder.ToString();
            }
            else
                text = reader.ReadToEnd();

            foreach (var exp in gloabalRemoves)
                text = text.Replace(exp, "");

            char[] newRow = { '\n' };
            string[] textAsList = text.Split(newRow);

            removeExps.AddRange(garbageLines);
            Match HasCrafting = Regex.Match(text, @"^\[(?:Applying|Removing) crafting effects\]\s*?$", RegexOptions.Multiline);
            if (HasCrafting.Length > 0)
            {
                removeExps.AddRange(craftingLines);
            }
            if (removeCombat) removeExps.AddRange(combatLines);

            formatReplacesOrdered = formatReplacesWithUserOverride(CustomEmotes);

            StringBuilder parsedText = new StringBuilder();
            string lineText = "";
            foreach (var line in textAsList)
            {
                lineText = line;
                if (!string.IsNullOrWhiteSpace(lineText))
                {
                    foreach (var exp in removeExps)
                        lineText = exp.Replace(lineText, "");

                    if (ServerMode == true)
                    {
                        foreach (var exp in serverReplacesOrdered)
                            lineText = exp.Item1.Replace(lineText, exp.Item2);
                    }

                    foreach (var exp in formatReplacesOrdered)
                        lineText = exp.Item1.Replace(lineText, exp.Item2);

                    if (!string.IsNullOrWhiteSpace(lineText))
                    {
                        lineText += "<br />";
                        parsedText.Append(lineText);
                    }
                }
            }

            reader.Close();
            
            text = HTMLPackageLog_Get(parsedText.ToString(), ServerName, ServerNameColor);
            return text;
        }
        public bool LineCount_Get(string ParsedNWNLog, int MinimumRowsCount)
        {
            Match LineCount = Regex.Match(ParsedNWNLog, @"<br />");

            if (LineCount.Length >= MinimumRowsCount)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private string HTMLPackageLog_Get(string ParsedNWNLog, string ServerName, string ServerNameColor)
        {
            string HTMLHeader = "<head>" +
                "<style>" +
                    ".logbody { background-color: #000000; font-family: Tahoma, Geneva, sans-serif; color: #FFFFFF; }";
            HTMLHeader += ".logheader { color: #";
            if (ServerNameColor != "")
            {
                HTMLHeader += ServerNameColor;
            }
            HTMLHeader += " }" +
                    ".default { color: #FFFFFF }" +
                    ".timestamp { color: #B1A2BD; }" +
                    ".actors { color: #8F7FFF; }" +
                    ".tells { color: #0F0; }" +
                    ".whispers { color: #808080; }" +
                    ".emotes { color: #ffaed6; }" +
                "</style>" +
            "</head>";

            string logTitle;
            if (ServerName != "")
            {
                logTitle = "<h4>[<span class='logheader'>" + ServerName + " Log</span>] ";
            }
            else
            {
                logTitle = "<h4>[<span class='logheader'>Log</span>] ";
            }
            logTitle += "<span class='actors'>Date/Time</span>: " + DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
            logTitle += "</h4>";
            string postLog = "</span></body></html>";
            return "<html>" + HTMLHeader + logTitle + ParsedNWNLog + "<body class='logbody'><span class='default'>" + postLog;
        }

        private List<Tuple<Regex, string>> formatReplacesWithUserOverride(string CustomEmotes)
        {
            string[] emotesArray = CustomEmotes.Split(',');
            List<Tuple<Regex, string>> formatReplacesOrderedReturn = new List<Tuple<Regex, string>>();

            formatReplacesOrderedReturn.AddRange(formatReplacesOrderedOne);
            formatReplacesOrderedReturn.AddRange(formatReplacesOrderedTwo);
            if (CustomEmotes.Length != 0)
            {
                List<Tuple<Regex, string>> additionalEmotesList = new List<Tuple<Regex, string>>();
                foreach (string emotePair in emotesArray)
                {
                    string theEmotePair = emotePair.Trim();
                    if (theEmotePair.Length == 2)
                    {
                        string tempLeftBracket = theEmotePair.Substring(0, 1);
                        string tempRightBracket = theEmotePair.Substring(1, 1);
                        string theRegEx;
                        theRegEx = "\\" + tempLeftBracket + "(?!([0-9]{2}\\:[0-9]{2}|Whisper|Tell)).*?\\" + tempRightBracket;

                        Tuple<Regex, string> theCustomEmote = new Tuple<Regex, string>(new Regex(@"(" + theRegEx + ")", RegexOptions.Compiled), "<span class='emotes'>$1</span>");
                        additionalEmotesList.Add(theCustomEmote);
                    }
                    else if (theEmotePair.Length == 1)
                    {
                        string tempBracket = theEmotePair.Substring(0, 1);
                        string theRegEx;
                        theRegEx = "\\" + tempBracket + "(?!([0-9]{2}\\:[0-9]{2}|Whisper|Tell)).*?\\" + tempBracket;

                        Tuple<Regex, string> theCustomEmote = new Tuple<Regex, string>(new Regex(@"(" + theRegEx + ")", RegexOptions.Compiled), "<span class='emotes'>$1</span>");
                        additionalEmotesList.Add(theCustomEmote);
                    }
                }
                formatReplacesOrderedReturn.AddRange(additionalEmotesList);
            }

            return formatReplacesOrderedReturn;
        }

        private List<Tuple<Regex, string>> formatReplacesOrdered = new List<Tuple<Regex, string>>();

        private List<Tuple<Regex, string>> formatReplacesOrderedOne = new List<Tuple<Regex, string>>
        {
            new Tuple<Regex, string> ( new Regex(@"\[{1}[A-z]{3}\s[A-z]{3}\s[0-9]{2}\s", RegexOptions.Compiled), "<span class='timestamp'>[" ),
            new Tuple<Regex, string> ( new Regex(@"\:{1}[0-9]*]{1}",RegexOptions.Compiled), "]</span>" ),
            // actors
            new Tuple<Regex, string>( new Regex(@"\]<\/span>((...).*: )",RegexOptions.Compiled), "]</span><span class='actors'>$1</span>" ),
            // tells
            new Tuple<Regex, string>( new Regex(@":\s?<\/span>\s?(\[Tell])(.*.*)",RegexOptions.Compiled), "</span><span class='tells'> $1:$2</span><br />"),
            // whispers 
            new Tuple<Regex, string>( new Regex(@":\s?<\/span>\s?(\[Whisper])(.*.*)",RegexOptions.Compiled), "</span><span class='whispers'> $1:$2</span><br />"),
        };


        private List<Tuple<Regex, string>> formatReplacesOrderedTwo = new List<Tuple<Regex, string>>
        {
            // emotes 
            new Tuple<Regex, string>( new Regex(@"(\*.*?\*)",RegexOptions.Compiled), "<span class='emotes'>$1</span>")
        };

        private List<String> gloabalRemoves = new List<String>
        {
            "\r",
            "[CHAT WINDOW TEXT] ",
        };

        private List<string> eventLines = new List<string>
        {
            "[Event]",
            "Minimum Tumble AC Bonus:",
            "No Monk/Shield AC Bonus:",
            "You are light sensitive!",
            "has left as a player..",
            "has joined as a player..",
            "has left as a game master..",
            "has joined as a game master..",
            "You are now in a Party PVP area.",
            "You are now in a No PVP area.",
            "Resting.",
            "Cancelled Rest.",
            "You used a key.",
            "Equipped item swapped out.",
            "You are portalling, you can't do anything right now",
            "Unknown Speaker: You are being moved to your last location, please wait...",
            "You are entering a new area!",
            "This container is persistent.",
            "This container is full",
            "You are too busy to barter now",
            "Player not found",
            "You cannot carry any more items, your inventory is full",
            "This is a trash, its contents may be purged at anytime",
            "Armor/Shield Applies: Skill ",
            "Your character has been saved",
            "New Value:",
            "Quick bar",
            "[ERROR TOO MANY INSTRUCTIONS]",
            "*** ValidateGFFResource sent by user.",
            "Modifying colours doesn't cost gold.",
            "Ignore the crafting roll and gold message for robes."
        };

        private static string timestampMatch = @".+?(?=.*)";

        private List<Regex> combatLines = new List<Regex>
        {
            new Regex(timestampMatch+@"\*{1}hit\*{1}.*\s\:\s\(\d{1,}\s[+-]\s\d{1,}\s\=\s\d{1,}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"damages\s.*\:\s{1}\d{1,}\s{1}\({1}\d{1,}\s{1}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\*{1}parried\*{1}.*\({1}\d{1,}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}[a-zA-Z]*\:{1}\s{1}Damage\s{1}[a-zA-Z]*\s{1}absorbs\s{1}.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\*{1}target concealed\:{1}.*\:{1}\s{1}\({1}\d{1,}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\*{1}critical hit\*\s{1}\:{1}\s{1}\({1}\d{1,}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\*{1}resisted\*\s{1}\:{1}\s{1}\({1}\d{1,}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"Immune\s{1}to\s{1}Critical\s{1}Hits\.{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\*{1}miss\*{1}.*\s\:\s\(\d{1,}\s{1}.*\d{1,}\s\=\s\d{1,}\)", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\*{1}success\*{1}\s{1}\:{1}\s{1}\(\d{1,}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\*{1}failure\*{1}.*\s\:\s{1}\({1}.*\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\:\s{1}Initiative\s{1}Roll\s{1}\:\s\d{1,}\s\:\s\(\d{1,}\s[+-]\s{1}\d{1,}\s{1}\={1}\s{1}\d{1,}\){1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\:{1}\s{1}Damage Immunity\s{1}absorbs.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\:{1}\s{1}Immune to Sneak Attacks\.{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\:{1}\s{1}Immune to Negative Levels\.{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\:{1}\s{1}Spell Level Absorption absorbs\s{1}\d{1,}.*\:{1}\s{1}\d{1,}.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}[a-zA-Z]*cast.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}[a-zA-Z]*uses.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}[a-zA-Z]*enables.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"[a-zA-Z]*\s{1}attempts\s{1}to\s{1}.*\:\s{1}.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"[a-zA-Z]*\:{1}\s{1}Healed\s{1}\d{1,}\s{1}hit.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"[a-zA-Z]*\:{1}\sImmune to [a-zA-Z]*.*\.{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}Dispel\s{1}Magic\s{1}\:{1}\s{1}[a-zA-z]*.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}Experience Points Gained\:{1}\s{1,}\d{1,}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"There are signs of recent fighting here...\*{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"Stale temporary properties detected, cleaning item\.{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}\[Check for loot\:{1}\s{1}\d{1,}.*\]{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}You.{1}ve reached your maximum level.\s{1}.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}Devastating Critical Hit!", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1,}Done resting\.{1}.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1,}You triggered a Trap!{1}.*", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}You cannot target a creature you cannot see or do not have a line of sight to\.{1}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}Weapon equipped as a one-handed weapon.", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}You cannot rest so soon after exerting yourself.", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}Equipping this armor has disabled your monk abilities.", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s{1}No resting is allowed in this area.", RegexOptions.Compiled),
            new Regex(timestampMatch+@"\s[A-z\s]*?enters rage.", RegexOptions.Compiled),
        };
        private List<Regex> garbageLines = new List<Regex>
        {
            new Regex(@"\[[A-z0-9\s]+\]\s.*?\:\s\[(?:Talk|Shout|Whisper|Tell)\]\s.*", RegexOptions.Compiled),
            new Regex(@"\[\w{3}\s\w{3}\s\d{2}\s\d{2}\:\d{2}:\d\d\]\s\[(?:Talk|Shout|Whisper|Tell)\]\s.*", RegexOptions.Compiled),
            new Regex(@"nwsync\:\s?Storage\s?at\s?[0-9:A-z\,= ]{10,}", RegexOptions.Compiled),
            new Regex(@"nwsync\:\s?Migrations\s?currently\s?applied\:\s?\d{1,}", RegexOptions.Compiled),
            new Regex(@"nwsync\:\s?Shard\s?\d{1,}\s?available,\sSpace\sUsed\:\s?\d{1,}\sKB", RegexOptions.Compiled),
            new Regex(@"Game\s?is\s?using\s?local\s?port\:\s?\d{1,}", RegexOptions.Compiled),
            new Regex(@"Error\:\s?\d{1,}", RegexOptions.Compiled),
            new Regex(@"GOG\:\s?Authentication\s?failed\:\s?\d{1,}", RegexOptions.Compiled),
            new Regex(timestampMatch+@"Script\s.*,\sOID\:.*,\sTag\:\s.*,\sERROR\:\sTOO MANY INSTRUCTIONS", RegexOptions.Compiled),
        };
        private List<Regex> craftingLines = new List<Regex>
        {
            new Regex(timestampMatch + @"\[(?:Applying|Removing) crafting effects\]\s*?"),
            new Regex(timestampMatch + @".*?\:\sJump\s\d+\s(?:part|colou?r)s\s(?:forward|backward)", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\sSelect\sthe\scolou?r\stype\sthat\syou\swant\sto\schange\.", RegexOptions.Compiled),
            new Regex(timestampMatch + @"Lost\sItem\:\s[A-z\s\(\)\d]+", RegexOptions.Compiled),
            new Regex(timestampMatch + @"Colou?r\sset\sto\:\s[A-z\s\(\)\d]+", RegexOptions.Compiled),
            new Regex(timestampMatch + @"Current Colou?r(\stype)?\:\s[A-z\s\(\)\d]+", RegexOptions.Compiled | RegexOptions.Singleline),
            new Regex(timestampMatch + @"Current\spart\:\s\d+(\(\d+\))?", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\s(?:Next|Previous|Change)\sColou?r", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\s(?:Next|Previous|Change)\sColou?r", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\sChange\s(?:Right|Left)?\s?[A-z]+", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\s(Next|Previous) part", RegexOptions.Compiled),
            /*
            // Try compound statements, then individual lines if they still remain.
            new Regex(timestampMatch + @".*?\:\sChange\s(?:Cloth|Metal|Leather)\s\d+" + timestampExactMatch + @".*?\:\sBack", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\sWhich\spart\sdo\syou\s\want\sto\schange\?(" + timestampExactMatch + @".*?\:\s(?:Right|Left)\s?[A-z]*?)?" + timestampExactMatch + @".*?\:\sBack", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\sWhat\sdo\syou\swant\sto\smodify\?(" + timestampExactMatch + @".*?\:\s(?:Armour|Weapon)\s?(?:Colours|Appearance))?" + timestampExactMatch + @".*?\:\sBack", RegexOptions.Compiled),
            // Fallback compounds
            new Regex(timestampMatch + @".*?\:\sWhich\spart\sdo\syou\s\want\sto\schange\?" + timestampExactMatch + @".*?\:\s((?:Right|Left)\s?[A-z]*?|Neck|Back|Belt|Helmet|Armour)", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\sChange\s(?:Cloth|Metal|Leather)\s\d+", RegexOptions.Compiled),
            new Regex(timestampMatch + @".*?\:\sWhat\sdo\syou\swant\sto\smodify\?" + timestampExactMatch + @".*?\:\s(?:Armour|Weapon)\s?(?:Colours|Appearance)", RegexOptions.Compiled),
            new Regex(@"", RegexOptions.Compiled),
            */
        };
        private List<Tuple<Regex, string>> serverReplacesOrdered = new List<Tuple<Regex, string>>
        {
            new Tuple<Regex, string> ( new Regex(@"(\-\-\-\-\sServer\sOptions\s\-\-\-\-)([^|]*)(\-\-\-\-\sEnd\sServer\sOptions\s\-\-\-\-)", RegexOptions.Compiled), "<span class='whispers'>$1</span><span class='tells'>$2</span><span class='whispers'>$3</span>" ),
            new Tuple<Regex, string> ( new Regex(@"\]\s(.*?)\s(Joined\sas\s(?:Game\sMaster|Player)\s\d+)", RegexOptions.Compiled), "] <span class='actors'>$1</span> $2" ),
            new Tuple<Regex, string> ( new Regex(@"\]\s(.*?)\s(Left\sas\sa\s(?:Game\sMaster|Player))\s(\(\d+\splayers\sleft\))", RegexOptions.Compiled), "] <span class='actors'>$1</span> $2 <span class='emotes'>$3</span>" ),
            new Tuple<Regex, string> ( new Regex(@"(Your cryptographic public identity is\:\s)(.*?)", RegexOptions.Compiled), "$1<span class='emotes'>$2</span>" ),
            new Tuple<Regex, string> ( new Regex(@"(Our\spublic\saddress\sas\sseen\sby\sthe\smasterserver\:)\s(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\:\d{1,5})", RegexOptions.Compiled), "$1 <span class='emotes'>$2</span>" ),
            new Tuple<Regex, string> ( new Regex(@"(Connection\sAttempt\smade\sby\s)(.*?)(?=(|$))", RegexOptions.Compiled), "<span class='whispers'>$1</span><span class='actors'>$2</span>" ),
            new Tuple<Regex, string> ( new Regex(@"(SpellLikeAbilityReady\: Could not find valid ability in list.*?)", RegexOptions.Compiled), "<span class='whispers'>$1</span>" ),
            new Tuple<Regex, string> ( new Regex(@"(Event\sadded\swhile\spaused\:\s*?EventId\:\s\d\s*?CallerId\:\s\d+\s*?ObjectId\:\s*?\d+)", RegexOptions.Compiled), "<span class='emotes'>$1</span>" ),
            new Tuple<Regex, string> ( new Regex(@"(Server Shutting Down)", RegexOptions.Compiled), "<span class='whispers'>$1</span>" ),
        };
    }
}
