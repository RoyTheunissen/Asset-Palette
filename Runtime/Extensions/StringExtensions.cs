namespace RoyTheunissen.AssetPalette.Extensions
{
    public static partial class StringExtensions
    {
        private const string HungarianPrefix = "m_";
        private const char Underscore = '_';
        public const char DefaultSeparator = ' ';

        private static bool IsExcludedSymbol(char symbol, char wordSeparator = DefaultSeparator)
        {
            return char.IsWhiteSpace(symbol) || char.IsPunctuation(symbol) || symbol == wordSeparator;
        }

        /// <summary>
        /// Gets the human readable version of programmer text, like a variable name.
        /// </summary>
        /// <param name="programmerText">The programmer text.</param>
        /// <returns>The human readable equivalent of the programmer text.</returns>
        public static string ToHumanReadable(
            this string programmerText,
            char wordSeparator = DefaultSeparator)
        {
            if (string.IsNullOrEmpty(programmerText))
                return programmerText;

            bool wasLetter = false;
            bool wasUpperCase = false;
            bool addedSpace = false;
            string result = "";

            // First remove the m_ prefix if it exists.
            if (programmerText.StartsWith(HungarianPrefix))
                programmerText = programmerText.Substring(HungarianPrefix.Length);

            // Deal with any miscellanneous spaces.
            if (wordSeparator != DefaultSeparator)
                programmerText = programmerText.Replace(DefaultSeparator, wordSeparator);

            // Deal with any miscellanneous underscores.
            if (wordSeparator != Underscore)
                programmerText = programmerText.Replace(Underscore, wordSeparator);

            // Go through the original string and copy it with some modifications.
            for (int i = 0; i < programmerText.Length; i++)
            {
                // If there was a change in caps add spaces.
                if ((wasUpperCase != char.IsUpper(programmerText[i])
                     || (wasLetter != char.IsLetter(programmerText[i])))
                    && i > 0 && !addedSpace
                    && !(IsExcludedSymbol(programmerText[i], wordSeparator) ||
                         IsExcludedSymbol(programmerText[i - 1], wordSeparator)))
                {
                    // Upper case to lower case.
                    // I added this so that something like 'GUIItem' turns into 'GUI Item', but that 
                    // means we have to make sure that no symbols are involved. Also check that there 
                    // isn't already a space where we want to add a space. Don't want to double space.
                    if (wasUpperCase && i > 1 && !IsExcludedSymbol(programmerText[i - 1], wordSeparator)
                        && !IsExcludedSymbol(result[result.Length - 2], wordSeparator))
                    {
                        // From letter to letter means we have to insert a space one character back.
                        // Otherwise it's going from a letter to a symbol and we can just add a space.
                        if (wasLetter && char.IsLetter(programmerText[i]))
                            result = result.Insert(result.Length - 1, wordSeparator.ToString());
                        else
                            result += wordSeparator;
                        addedSpace = true;
                    }

                    // Lower case to upper case.
                    if (!wasUpperCase)
                    {
                        result += wordSeparator;
                        addedSpace = true;
                    }
                }
                else
                {
                    // No case change.
                    addedSpace = false;
                }

                // Add the character.
                result += programmerText[i];

                // Capitalize the first character.
                if (i == 0)
                    result = result.ToUpper();

                // Remember things about the previous letter.
                wasLetter = char.IsLetter(programmerText[i]);
                wasUpperCase = char.IsUpper(programmerText[i]);
            }

            return result;
        }

        /// <summary>
        /// Splits up a string like Footstep_02 into Footstep_ (root) and 02 (numberSuffix).
        /// </summary>
        public static void GetNumberSuffix(this string name, out string root, out string numberSuffix)
        {
            GetNumberSuffix(name, false, out root, out numberSuffix);
        }

        /// <summary>
        /// Splits up a string like Footstep_02 into Footstep_ (root) and 02 (numberSuffix).
        /// </summary>
        public static void GetNumberSuffix(
            this string name, bool includeSeparatorsInNumber, out string root, out string numberSuffix)
        {
            numberSuffix = string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                root = string.Empty;
                return;
            }

            for (int i = name.Length - 1; i >= 0; i--)
            {
                if (char.IsNumber(name[i]) || includeSeparatorsInNumber && (name[i] == ' ' || name[i] == '_'))
                {
                    numberSuffix = name[i] + numberSuffix;
                    continue;
                }

                break;
            }

            root = name.Substring(0, name.Length - numberSuffix.Length);
        }

        /// <summary>
        /// Splits up a string with parentheses like Footstep (New) into Footstep (root) and (New) (parentheses).
        /// </summary>
        public static void GetParenthesesSuffix(this string name, out string root, out string parenthesesSuffix)
        {
            root = name;
            parenthesesSuffix = string.Empty;

            if (string.IsNullOrEmpty(name))
                return;

            if (!name.EndsWith(")"))
                return;

            int parenthesesStart = name.LastIndexOf("(");
            root = name.Substring(0, parenthesesStart);
            parenthesesSuffix = name.Substring(parenthesesStart);
        }

        public static string RemovePrefix(this string name, string prefix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(prefix))
                return name;

            if (!name.StartsWith(prefix))
                return name;

            return name.Substring(prefix.Length);
        }

        public static string RemoveSuffix(this string name, string suffix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(suffix))
                return name;

            if (!name.EndsWith(suffix))
                return name;

            return name.Substring(0, name.Length - suffix.Length);
        }

        public static bool TryGetNumberSuffix(this string name, out int number)
        {
            number = -1;

            if (!name.EndsWith(')'))
                return false;

            int openingParenthesis = name.LastIndexOf('(');
            if (openingParenthesis == -1)
                return false;

            string suffix = name.Substring(openingParenthesis + 1);
            suffix = suffix.Substring(0, suffix.Length - 1);

            bool hasValidNumber = int.TryParse(suffix, out int parsedNumber);
            if (!hasValidNumber)
                return false;

            number = parsedNumber;
            return true;
        }

        public static string SetNumberSuffix(this string name, int number)
        {
            if (!name.EndsWith(')'))
                return $"{name} ({number})";

            int openingParenthesis = name.LastIndexOf('(');
            if (openingParenthesis == -1)
            {
                // Remove the closing parenthesis and any trailing spaces. 
                name = name.Substring(0, name.Length - 1);
                name = name.TrimEnd(' ');
                return $"{name} ({number})";
            }

            return $"{name.Substring(0, openingParenthesis)}({number.ToString()})";
        }
    }
}
