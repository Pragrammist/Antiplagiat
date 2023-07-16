using System.Globalization;
using System;
using System.Linq;
using System.Text;
using StackExchange.Redis;
using System.Runtime.InteropServices;
using System.IO.Hashing;
using BenchmarkDotNet.Attributes;
using DeepMorphy;
using BenchmarkDotNet.Running;
using static HashAlgConts;

//BenchmarkRunner.Run<MyBenchTest>();

var text1 = System.IO.File.ReadAllText("t1.txt");
using var indexer = new Indexer();
//var indexed =  await indexer.IndexText(text1, "t1");

var matchesResult =  await indexer.SearchMatchesInIndexes(@"C# является мощным и эффективным языком программирования, который может использоваться для создания широкого спектра приложений, от простых консольных программ до сложных веб-приложений и игр");

var summary = await indexer.SummaryOfSearchingResult(matchesResult);

//var matchesResult = await indexer.SearchMatchesInIndexes(text1);

foreach(var match in matchesResult)
{
    
}






// var text = System.IO.File.ReadAllText("t.txt");
// foreach(string? threeWord in text.GetHashsByThreeWords())
// {
//     if(threeWord == null)
//         continue;
    
//     System.IO.File.AppendAllLines("t2.txt", new string[] { threeWord.ToString()});
// }


public record IndexerSearchingResult(int OverallInputTextHashesCount, int OverallDocumentHashesCount, string DocumentId, int MatchesCount, double MatchesPercentOfOverallInputTextHashesCount, double MatchesPercentOfOverallDocumentHashesCount);

public record IndexerSummaryResult(double Citations, double Plagiats, double Unique, IEnumerable<string> DocCitations, IEnumerable<string> DocPlagiats);
// {
//     public double Citation {g}
// }

public class Indexer : IDisposable
{
    ConnectionMultiplexer _redis;
    IDatabase _db;
    private bool disposedValue;

    public Indexer()
    {
        _redis = ConnectionMultiplexer.Connect("localhost");
        _db = _redis.GetDatabase();
    }


    Task ForeachInTextWithoutHashing(string text, Func<string, Task> MethodInnerForeach)
    {
        foreach(string? threeWord in text.GetHashsByThreeWords())
        {
            if(threeWord == null)
                continue;

            var taskRes = MethodInnerForeach(threeWord);
            taskRes.ConfigureAwait(false);
            taskRes.Wait();
            
        }
        return Task.CompletedTask;
    }


    Task ForeachInTextWithHashing(string text, Func<long, Task> MethodInnerForeach)
    {
        foreach(long? hash in text.GetHashsByThreeWords())
        {
            if(hash == null || hash == 0)
                continue;
            var taskRes = MethodInnerForeach(hash.Value);
            taskRes.ConfigureAwait(false);
            taskRes.Wait();
            
        }
        return Task.CompletedTask;
    }


    public async Task<IndexerSummaryResult> SummaryOfSearchingResult(IEnumerable<IndexerSearchingResult> searchingResult)
    {
        var citationSearchingResult = searchingResult.Where(k => k.MatchesPercentOfOverallDocumentHashesCount >= 0.01 && k.MatchesPercentOfOverallDocumentHashesCount < 0.15);

        //var fragmentSearchingResult = searchingResult.Where(k => k.MatchesPercentOfOverallDocumentHashesCount >= 0.15 && k.MatchesPercentOfOverallDocumentHashesCount < 0.5);

        var plagiatSearchingResults = searchingResult.Where(k => k.MatchesPercentOfOverallDocumentHashesCount >= 0.15); //0.5

        var documentsIndexedCount = (await _db.SetMembersAsync(REDIS_KEY_FOR_DOCUMENTS_SET)).Length;
        var citationPercent = citationSearchingResult.Sum(c => c.MatchesPercentOfOverallInputTextHashesCount);
        var plagiatPercent = plagiatSearchingResults.Sum(c => c.MatchesPercentOfOverallInputTextHashesCount);
        var uniquePercent = 100 - plagiatPercent;
        var summaryResult = new IndexerSummaryResult(citationPercent, plagiatPercent, uniquePercent, citationSearchingResult.Select(c => c.DocumentId), plagiatSearchingResults.Select(c => c.DocumentId));
        return summaryResult;

    }

    public async Task<int> IndexText(string text, string documentId)
    {
        int counter = 0;

        await _db.SetAddAsync(REDIS_KEY_FOR_DOCUMENTS_SET, documentId);

        await ForeachInTextWithoutHashing(text, async (threeWord) =>
        {
            var members = await _db.SetMembersAsync(threeWord);

            string? member = members.FirstOrDefault(m => m.ToString().Contains(documentId));
            int numMatch = 0;

            if(member is not null)
            {
                await _db.SetRemoveAsync(threeWord, member);
                var numMatchStr = member.Split(":").ElementAt(1);
                numMatch = int.Parse(numMatchStr);
            }

            numMatch++;
            var documentWithNumOfMatch = $"{documentId}:{numMatch}";
            var added = await _db.SetAddAsync(threeWord, documentWithNumOfMatch);
            
            if(added)
                counter++;
        });

        if(counter != 0)
            await _db.StringSetAsync(documentId, counter);
        
        return counter;
        
    }


    public async Task<IEnumerable<IndexerSearchingResult>> SearchMatchesInIndexes(string text)
    {
        
        var matchedRes = new Dictionary<string,int>();
        int inputTextHashesCount = 0;
        await ForeachInTextWithoutHashing(text, async (threeWord) => {

            var documents =  await _db.SetMembersAsync(threeWord);
            inputTextHashesCount++;
            foreach(string? documentIdAndNum in documents)
            {
                if(documentIdAndNum is null)
                    continue;
                
                var documentId = documentIdAndNum.Split(":").First();
                

                if(!matchedRes.Keys.Contains(documentId))
                    matchedRes[documentId] = 0;

                matchedRes[documentId]++;

                
            }
        });
        var res = matchedRes.Select(kvp => {
            var documentHashesCount = (int)_db.StringGet(kvp.Key);
            return new IndexerSearchingResult(
                OverallInputTextHashesCount: inputTextHashesCount,
                OverallDocumentHashesCount: documentHashesCount,
                DocumentId:kvp.Key,
                MatchesCount: kvp.Value,
                MatchesPercentOfOverallInputTextHashesCount: Math.Round((double)kvp.Value/(double)inputTextHashesCount * 100, 2, MidpointRounding.AwayFromZero),
                MatchesPercentOfOverallDocumentHashesCount: Math.Round((double)kvp.Value/(double)documentHashesCount * 100, 2, MidpointRounding.AwayFromZero)
            ); 
        }).Where(indexRes => indexRes.MatchesPercentOfOverallDocumentHashesCount > 0);
        return res;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                _redis.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~Indexer()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}



public static class HashAlgConts
{
    public const int ALPHABET_SIZE = 33;
    public const int MAX_WORD_LENGTH = 30;
    public const string REDIS_KEY_FOR_DOCUMENTS_SET = "documents";
}


public class TextFilterNotOp
{
    MorphAnalyzer morph = new MorphAnalyzer();
    Crc64 hashAlg = new Crc64();
    IEnumerable<string> FilterText(string inputText) {

        const string allowedSymbols = "йцукенгшщзхъфывапролджэячсмитьбюёЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮЁ";
        string[] notAllowedMorphTags = new string[] {"предл", "мест", "межд", "союз"  };
        var filteredWordBySymbols = inputText
            .Split(new char[] { ' ', '\n'},StringSplitOptions.RemoveEmptyEntries) // split by empties and new lines
            .Select(str => str.Where(sym => allowedSymbols.Contains(sym))) // filter symbols that dosn't contains any in rus symb
            .Where(str => str.Count() > 1) // filter for word
            .Select(arr => new string(arr.ToArray())); // convert IEnumerable<char> to string
        var filteredWordByMorph = morph.Parse(filteredWordBySymbols) // parse words for morph-class
            .Where(s => !notAllowedMorphTags.Any(notAllowedMorphTag => s.BestTag.Grams.Contains(notAllowedMorphTag))) // filter for words their type
            .Select(s => s.BestTag.HasLemma ? s.BestTag.Lemma: s.Text);
        return filteredWordByMorph;
    }


    IEnumerable<long> HashWords(IEnumerable<string> words)
    {
        int i = 1;
        StringBuilder threeWord = new StringBuilder();
        foreach(var word in words)
        {
            threeWord.Append(word);        
            threeWord.Append(' ');
            if(i == 3)
            {
                var threeWordSpan = threeWord.ToString().AsSpan();
                var threeWordHashed = Get64BitHashForStr(threeWordSpan);
                var longTypeHash = BitConverter.ToInt64(threeWordHashed);
                threeWord.Clear();
                i = 1;
                yield return longTypeHash;
            }
            i++;
        }
    }
    byte[] Get64BitHashForStr(ReadOnlySpan<char> str)
    {
        var bytes = MemoryMarshal.AsBytes(str);
        hashAlg.Append(bytes);
        return hashAlg.GetHashAndReset();
    }
}


public static class StringSplitExtensions
{
    public static TextSplitWithFilerEnumerator GetHashsByThreeWords(this string str)
    {
        // LineSplitEnumerator is a struct so there is no allocation here
        return new TextSplitWithFilerEnumerator(str.AsSpan());
    }

    // Must be a ref struct as it contains a ReadOnlySpan<char>
    public ref struct TextSplitWithFilerEnumerator
    {
        private ReadOnlySpan<char> _allStr;
        ThreeWordHasher _wordHasher = new ThreeWordHasher();
        Crc64 hashAlg = new Crc64();
        public TextSplitWithFilerEnumerator(ReadOnlySpan<char> str)
        {
            _allStr = str;
            Current = default;
        }
        
        
        ReadOnlySpan<char> allCaseAllowedSymbols = "йцукенгшщзхъфывапролджэячсмитьбюёЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮЁ".AsSpan();
        //ReadOnlySpan<char> allowedSymbolsInLower = "йцукенгшщзхъфывапролджэячсмитьбюё";
        CultureInfo rusCultureInfo = CultureInfo.GetCultureInfo("ru-RU");

        ReadOnlySpan<byte> bufferForHash;

        string? bufferForThreeWords;

        char[] bufferForToLowerOperation = new char[MAX_WORD_LENGTH];
        Span<string> notAllowedWords = new string[]
        {
            "он", "на", "который", "ним", "нем", "ней", "до", "после", "через", "из", "за", "от",
            "по", "ради", "для", "по", "со", "без", "которая",
            "близ", "во", "под", "ко", "над", "об", 
            "обо", "от", "ото", "перед", "передо", "пред", 
            "предо", "подо", "при","про", 
            "именно", "также", "то", "благодаря",
            "будто", "вроде", "вопреки", "ввиду", 
            "вследствие", "да", "еще", "дабы", "даже", "же", 
            "ежели", "если", "бы", "то", "затем",
            "зато", "зачем", "значит", "поэтому", "притом",
            "таки", "следовательно", "ибо", "вдобавок", "или",
            "кабы", "как", "скоро", "словно", "только", "так",
            "когда", "коли", "тому", "кроме", "того", "ли",
            "либо", "лишь", "тем", "нежели", "столько",
            "сколько", "невзирая", "независимо", "несмотря", 
            "ни", "но", "однако", "особенно", "оттого", "что",
            "отчего", "подобно", "пока", "покамест", "покоду",
            "после", "поскольку", "потому", "почему", "чем", 
            "прежде","всем", "том", "причем", "пускай", "пусть",  
            "пор", "более", "тогда", "есть", "тоже", "чуть", "виду", 
            "можно", "было", "которые", "могут", "могу", "будет",
            "быть", "был", "будешь",  "будет", "будем", 
            "будете", "будут", "был", "была", "были",
            "мог", "мочь", "могу", "можешь", "может",
            "можем", "можете", "могут", "мог", "могла", 
            "могло", "могли", "все", "всех", 
            "всеми", "выше", "возможность", "возможным",
            "делать", "делают", "делаете",
            "делаем", "делало", "делал", 
            "сделать", "сделают", "сделаете",
            "сделаем", "сделало", "сделал",
            "позовлять", "позволяю", "позволяешь",
            "позволяют", "позволяете", "позволял",
            "позволяла", "позволяло", "позволяли",
            "позволил", "позволила", "позволило",
            "позволили", "позволить", "иметь",
            "имею", "имеешь", "имеет", "имеем", "имеете", 
            "имеют", "имел", "имела", "имело", "имели", "самой", 
            "имеющий", "имевший", "самый", "самая", "самое", "самую", 
            "самого", "самых", "самому", "самым", "самым", "самой", 
            "самою", "самым", "самыми", 
            "позволяющий", "позволявший", "позволяемый",
            "точно", "хотя", "чтоб", "чтобы", 
            "мы", "вы", "он", "она", "оно", "они",
            "нас", "вас", "его", "ее", "их",
            "нам", "вам", "ему", "ей", "им",
            "нами", "вами", "ею", "ими",
            "ней", "нем", "них", "меня", 
            "мне", "меня", "мной", "мне", 
            "себе", "собой", "собою", "себя",
            "различный", "различного",
            "различным", "различная", 
            "различной", "различной", "различное",
            "различному", "различным",
            "различном", "такой", "такого",
            "такому", "таким", "таком",
            "такая", "такую", "такою",
            "такое", "такие", 
            "таких", "таким", "такие",
            "такими", "разный",
            "разного", "разному",
            "разный",
            "разным", "разном",
            "разная", "разной",
            "разную", "разною",
            "разное", "разных", 
            "разному", "разные",
            "разными",
            "кто",  "кого", "кому", "кого", 
            "кем", "ком", "чего", "чему",
            "ты", "тебя", "тебе", "тобой",
            "твой", "твое", "твоя", "твои",
            "чей", "чьи", "чья", "чьи",
            "какой", "какое", "какая", "какие",
            "всякий", "всякое", "всякая", "всякие",
            "любой", "любое", "любая", "любые",
            "этот", "это", "эта", "эти",
            "твоего", "твоей", "твоих",
            "чьего", "чьей", "чьих",
            "какого", "каких", 
            "всякого", "всякой", "всяких",
            "лобого", "любых",
            "этого", "этой", "этих",
            "твоейму", "твоим",
            "чьему", "чьим",
            "какому", "каким",
            "всякому", "всяким",
            "любому", "любым",
            "этому", "этим", 
            "твою", "чьими",
            "какими","всякими", "любыми",
            "этими", 
            "твоем", "чьем",
            "каком", "всяком",
            "этом", "сколько", 
            "скольких", "скольким", 
            "сколькими", 
        };
        // Needed to be compatible with the foreach operator
        public TextSplitWithFilerEnumerator GetEnumerator() => this;
      
        public bool MoveNext()
        {
            if (_allStr.Length == 0) // Reach the end of the string
                return false;

            var currentStr = _allStr;

            var index = currentStr.IndexOfAnyExcept(allCaseAllowedSymbols);

            var separator = index != -1 ? currentStr.Slice(index, 1) : ReadOnlySpan<char>.Empty;

            var word = index != -1 ? currentStr.Slice(0, index) : currentStr;

            var filteredWord = word.Length > 1 && word.Length < MAX_WORD_LENGTH  ? word : ReadOnlySpan<char>.Empty;

            var wordIsAbbreviation = false;

            foreach(var upperCaseSym in allCaseAllowedSymbols[ALPHABET_SIZE..])
            {
                if(filteredWord.Length == 0)
                    break;

                int lastSymIndex = filteredWord.Length - 1;
                int middleSymIndex = (filteredWord.Length / 2);
                if(upperCaseSym == filteredWord[lastSymIndex] || upperCaseSym == filteredWord[middleSymIndex])
                {
                    wordIsAbbreviation = true;
                }
            }
            if(!wordIsAbbreviation)
            {
                var countWrited = filteredWord.ToLower(bufferForToLowerOperation, rusCultureInfo);
                var bufferForToLowerOperationSpan = bufferForToLowerOperation.AsSpan();
                filteredWord = bufferForToLowerOperationSpan[0..countWrited];
            }
            bool isNotAllowedWord = false;
            foreach(var fitlerWord in notAllowedWords)
            {
                if(filteredWord.Length == 0)
                    break;
                var spanFilterWord = fitlerWord.AsSpan();
                if(spanFilterWord.Length == filteredWord.Length)
                {
                    bool wordAreEqual = true;
                    for(int i = 0; i < filteredWord.Length; i++)
                    {
                        if(spanFilterWord[i] != filteredWord[i])
                        {
                            wordAreEqual = false;
                            break;
                        }

                    }
                    if(wordAreEqual)
                    {
                        isNotAllowedWord = true;
                        break;
                    }
                    
                }
            }
            
            filteredWord = isNotAllowedWord ? ReadOnlySpan<char>.Empty : filteredWord;
            
            _wordHasher.GetHashedWord(word:filteredWord, hashedWord: ref bufferForHash, threeWords: ref bufferForThreeWords);
            Current = new ThreeHashedWordsEntry(bufferForHash, bufferForThreeWords, separator);

            _allStr = index != -1 ? currentStr.Slice(index + 1) :  ReadOnlySpan<char>.Empty;

            return true;
        }
        public ThreeHashedWordsEntry Current { get; private set; }
    }


    public ref struct ThreeWordHasher
    {
        public ThreeWordHasher()
        {
            
        }
        Span<char> bufferForHashing = new char[MAX_WORD_LENGTH * 3].AsSpan();
        int wordCount = 0;
        int bufferForHashingPointer = 0;
        int firstWordLength = 0;
        int secondWordLength = 0;
        int thirdWordLength = 0;
        Crc64 hashAlg = new Crc64();
        

        public void GetHashedWord(ReadOnlySpan<char> word, ref ReadOnlySpan<byte> hashedWord, ref string? threeWords)
        {
            if(word.Length == 0)
                return;

            wordCount++;
            
            if(wordCount == 1)
            {
                firstWordLength = word.Length;
            }
            if(wordCount == 2)
            {
                secondWordLength = word.Length;
            }
            foreach(var sym in word)
            {
                bufferForHashing[bufferForHashingPointer] = sym;
                bufferForHashingPointer++;
            }

            if(wordCount == 3)
            {
                threeWords = bufferForHashing[..bufferForHashingPointer].ToString();
                var bytesToHash = MemoryMarshal.AsBytes(threeWords.AsSpan());
                hashAlg.Append(bytesToHash);
                hashedWord = hashAlg.GetHashAndReset();

                thirdWordLength = word.Length;
                int firstWordCounter = 0;
                int lengthIterators = bufferForHashingPointer - thirdWordLength;
                for(int i = firstWordLength; i < lengthIterators; i++)
                {

                    bufferForHashing[firstWordCounter] = bufferForHashing[i];
                    firstWordCounter++;
                    
                }
                int secondWordCounter = firstWordCounter;
                int lengthIterators2 = bufferForHashingPointer;
                for(int i = firstWordLength + secondWordLength; i < lengthIterators2; i++)
                {
                    bufferForHashing[secondWordCounter] = bufferForHashing[i];
                    secondWordCounter++;
                }
                
                for(int i = bufferForHashingPointer - firstWordLength; i < bufferForHashing.Length; i++)
                {
                    bufferForHashing[i] = '\0';
                }
                
                bufferForHashingPointer = bufferForHashingPointer - firstWordLength;
                wordCount--;
                firstWordLength = secondWordLength;
                secondWordLength = thirdWordLength;
            }
        
            
        }
    }


    public readonly ref struct ThreeHashedWordsEntry
    {
        public ThreeHashedWordsEntry(ReadOnlySpan<byte> threeHashedWords, string? threeWord, ReadOnlySpan<char> separator)
        {
            ThreeHashedWords = threeHashedWords;
            Separator = separator;
            ThreeWord = threeWord;
        }

        public ReadOnlySpan<byte> ThreeHashedWords { get; }
        public ReadOnlySpan<char> Separator { get; }

        public string? ThreeWord { get; }

        // This method allow to deconstruct the type, so you can write any of the following code
        // foreach (var entry in str.SplitLines()) { _ = entry.Line; }
        // foreach (var (line, endOfLine) in str.SplitLines()) { _ = line; }
        // https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/deconstruct?WT.mc_id=DT-MVP-5003978#deconstructing-user-defined-types
        public void Deconstruct(out ReadOnlySpan<byte> line, out ReadOnlySpan<char> separator)
        {
            line = ThreeHashedWords;
            separator = Separator;
        }

        // This method allow to implicitly cast the type into a ReadOnlySpan<char>, so you can write the following code
        // foreach (ReadOnlySpan<char> entry in str.SplitLines())
        public static implicit operator ReadOnlySpan<byte>(ThreeHashedWordsEntry entry) => entry.ThreeHashedWords;

        public static implicit operator string? (ThreeHashedWordsEntry entry) => entry.ThreeWord;

        public static implicit operator long?(ThreeHashedWordsEntry entry) => entry.ThreeHashedWords.Length > 0 ? BitConverter.ToInt64(entry.ThreeHashedWords) : null;
    }
}





[MemoryDiagnoser]
public class MyBenchTest : IDisposable
{
    readonly string testString;
    Indexer indexer = new Indexer();
    public MyBenchTest()
    {
        testString = System.IO.File.ReadAllText("/home/f/Documents/dotnet-app/AntiPlagiatIndexing/t1.txt");
        //testString = testString + " " + testString + " " + testString + " " + testString + " " + testString + " " + testString + " " + testString + " " + testString + testString + " " + testString;
        //testString = testString + testString + testString;
        
        
    }
    

    


    
    public async Task IndexTextTest()
    {
        await indexer.IndexText(testString, "1");
    }


    [Benchmark]
    public void FilteringOp() 
    {
        foreach(ReadOnlySpan<byte> hash in testString.GetHashsByThreeWords())
        {}
    }

    public void Dispose()
    {
        indexer.Dispose();
    }
}   