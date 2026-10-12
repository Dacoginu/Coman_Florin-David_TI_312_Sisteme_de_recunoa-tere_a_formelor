using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WordFrequency;

string root = @"C:\Users\david\Desktop\Morariu\Reuters\Reuters_34";
string stopPath = @"C:\Users\david\Desktop\Morariu\Documentation\stopwords.txt";
string outDir = @"C:\Users\david\Desktop\Morariu";

HashSet<string> stopwords = File.ReadAllLines(stopPath)
    .Select(w => w.Trim().ToLower())
    .Where(w => w.Length > 0)
    .ToHashSet();

PorterStemmer stemmer = new PorterStemmer();

// un articol = frecventele lui + topicurile + folderul
var docs = new List<(Dictionary<string, int> Freq, List<string> Topics, string Folder)>();

foreach (string folder in new[] { "Training", "Testing" })
{
    foreach (string file in Directory.GetFiles(Path.Combine(root, folder), "*.xml"))
    {
        XDocument doc = XDocument.Load(file);
        string title = doc.Root!.Element("title")?.Value ?? "";
        string text = doc.Root.Element("text")?.Value ?? "";

        List<string> topics = doc.Descendants("codes")
            .Where(c => (string?)c.Attribute("class") == "bip:topics:1.0")
            .Elements("code")
            .Select(c => (string)c.Attribute("code")!)
            .ToList();

        var freq = new Dictionary<string, int>();
        foreach (Match m in Regex.Matches((title + " " + text).ToLower(), "[a-z]+"))
        {
            string w = m.Value;
            if (w.Length <= 1 || stopwords.Contains(w)) continue;
            w = stemmer.StemWord(w);
            freq[w] = freq.GetValueOrDefault(w) + 1;
        }

        docs.Add((freq, topics, folder));
    }
}

// dictionarul global: toate cuvintele, sortate alfabetic, cu indexul lor
List<string> dictionary = docs.SelectMany(d => d.Freq.Keys).Distinct().OrderBy(w => w, StringComparer.Ordinal).ToList();
Dictionary<string, int> index = new Dictionary<string, int>();
for (int i = 0; i < dictionary.Count; i++) index[dictionary[i]] = i;

foreach (string folder in new[] { "Training", "Testing" })
{
    string outPath = Path.Combine(outDir, folder + ".txt");

    using (var writer = new StreamWriter(outPath, false, Encoding.UTF8))
    {
        foreach (string w in dictionary)
            writer.WriteLine("@attribute " + w);

        writer.WriteLine("@data");

        foreach (var d in docs.Where(d => d.Folder == folder))
        {
            string vector = string.Join(" ", d.Freq
                .Select(kv => (Pos: index[kv.Key], Count: kv.Value))
                .OrderBy(x => x.Pos)
                .Select(x => x.Pos + ":" + x.Count));

            writer.WriteLine(vector + " # " + string.Join(" ", d.Topics) + " # " + d.Folder);
        }
    }

    Console.WriteLine(folder + ": " + docs.Count(d => d.Folder == folder) + " articole -> " + outPath);
}

Console.WriteLine("Cuvinte in dictionar: " + dictionary.Count);