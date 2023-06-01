using System.Runtime.InteropServices;
using System.Collections.Immutable;
using System.Linq;
using System.Collections;
using System.Text;
using System.Security.Cryptography;
using Cyriller;




// unsafe
// {
//     var str = "указ президент право";
//     SplitWithSpanImpl(str);

    
//     var splitedStr = str.Split();
//     Array.Sort(splitedStr);
//     var sortedStr = string.Join(" ", splitedStr);
//     var strSize = sizeof(string) + str.Length;
//     Console.WriteLine("string size: {0}", strSize);

//     var hashFromHashAlg = MD5.HashData(Encoding.UTF8.GetBytes(sortedStr));
//     Console.WriteLine("Length of hash from hash alg: {0}", hashFromHashAlg.Length);

    
    
    
    
// }

var str = "";
Console.WriteLine(str.Split().Length);
IndexDocumentForSearching(str, 0);
void IndexDocumentForSearching(ReadOnlySpan<char> str, int documentId /**/)
{
    int i = 0;
    int lastSlicedIndex = 0;
    bool nextAnotherWord = false;
    
    var firstSliceIndex = 0;
    var secondSliceIndex = 0;
    var thirdSliceIndex = 0;

    foreach(var symbol in str)
    {
        var isLastSymb = i == str.Length - 1;
        
        if(nextAnotherWord || isLastSymb)
        {
            var sliceLength = isLastSymb ? i - lastSlicedIndex + 1 : i - lastSlicedIndex - 1; // так без

            firstSliceIndex = secondSliceIndex; //
            secondSliceIndex = thirdSliceIndex;
            thirdSliceIndex = lastSlicedIndex;

            ReadOnlySpan<char> currentWord = default;
            if(secondSliceIndex != 0 && thirdSliceIndex != 0)
                currentWord = str.Slice(firstSliceIndex, (secondSliceIndex - firstSliceIndex) + (thirdSliceIndex - secondSliceIndex) + sliceLength);
            
            var hashedCurrentWords = MD5.HashData(MemoryMarshal.AsBytes(currentWord)).AsSpan();

            


            lastSlicedIndex = i;
            nextAnotherWord = false;
            
        }
        nextAnotherWord = symbol == ' ';
        i++;
        
    }
}



// void SortThreeWordString(ReadOnlySpan<char> str)
// {
//     int i = 0;
//     int lastSlicedIndex = 0;
//     bool nextAnotherWord = false;
//     var wordTakedCount = 0;
    
    
//     ReadOnlySpan<char> thirdLastSlicedWord = default;
//     ReadOnlySpan<char> secondLastSlicedWord = default;
//     ReadOnlySpan<char> firstLastSlicedWord = default;

//     foreach(var symbol in str)
//     {
//         var isLastSymb = i == str.Length - 1;
        
//         if(nextAnotherWord || isLastSymb)
//         {
            
//             var sliceLength = isLastSymb ? i - lastSlicedIndex + 1 : i - lastSlicedIndex - 1; // так без

//             firstLastSlicedWord = secondLastSlicedWord;
//             secondLastSlicedWord = thirdLastSlicedWord;
//             thirdLastSlicedWord = str.Slice(lastSlicedIndex, sliceLength);
            
            

            

//             lastSlicedIndex = i;
//             nextAnotherWord = false;
//             wordTakedCount++;
            
//         }
//         nextAnotherWord = symbol == ' ';
//         i++;
        
//     }
// }
//var str = "указ президент право";
//const string TEST_SEARCH_STRING_OR_IT_IS_DOCUMENT = @"указ президент право";
//const string ALLOWED_SYMBOLS = @"йцукенгшщзхъфывапролджэячсмитьбю";
//var words = TEST_SEARCH_STRING_OR_IT_IS_DOCUMENT.Split();
//Console.WriteLine(words.Length);

//const int DOCUMENT_ID = 0;

// var indexedDocument = Alg(words, DOCUMENT_ID);


// var keys = indexedDocument.Keys.Select(s => s);

// foreach(var key in keys)
// {
//     Console.WriteLine("{0}: {1}", key, indexedDocument[key].First());
// }

// static int GetHashForString(string str) => BitConverter.ToInt32(SHA1.HashData(Encoding.UTF8.GetBytes(str)));

// static int GetHashForIntArray(int[] arr) => BitConverter.ToInt32(SHA1.HashData(ConvertToBytes(arr).ToArray()));

// static IEnumerable<byte> ConvertToBytes(int[] arr)
// {

    
//     foreach(var num in arr)
//     {
//         var bytes = BitConverter.GetBytes(num);
//         foreach(var b in bytes)
//         {
//             yield return b;
//         }
        
//     }
// }

// static Dictionary<int, int[]> Alg(string[] str, int documentId)
// {
//     // данная константа нужна чтобы можно было массив слов обрабатывать по три, пример: 
//     // "реакция происходить моментально примере", это входная строка, 
//     // где убраны падежы и второстепенные слова и символы
//     // перебор будет следующим:
//     // "реакция происходить моментально"
//     // "происходить моментально примере"
    
    
//     var indexedDocumentOrText = new Dictionary<int, int[]>();
//     const int COUNT_IN_ONE_ITERATION = 3;
//     var strLength = str.Length;

//     for(int i = 0; i <= strLength - COUNT_IN_ONE_ITERATION; i++)
//     {
//         var threeHashedStringsArray = new int[COUNT_IN_ONE_ITERATION];
//         var indexOfthirdHashedStringsArray = 0;
//         for(int j = i; j < i + COUNT_IN_ONE_ITERATION; j++)
//         {
//             threeHashedStringsArray[indexOfthirdHashedStringsArray] = GetHashForString(str[j]);
//             indexOfthirdHashedStringsArray++;
//         }
//         Array.Sort(threeHashedStringsArray);

//         var hashOfJoinedDocuments = GetHashForIntArray(threeHashedStringsArray);
        
//         indexedDocumentOrText[hashOfJoinedDocuments] = new int[] { documentId };
//     }
//     return indexedDocumentOrText;
// }

struct WhereWas
{
    public int DocumentId { get; set; }
}