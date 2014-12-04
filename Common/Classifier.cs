using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common
{
    public class Classifier
    {

        static Lazy<Dictionary<string, int>> WordScores = new Lazy<Dictionary<string, int>>(() =>
                System.IO.File.ReadLines(System.Configuration.ConfigurationManager.AppSettings["SentientListPath"])
                .Select(x => x.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .ToDictionary(x => x[0], x => int.Parse(x[1])));

        static Lazy<BasicClassifier.Classifier> NielsenClassify = new Lazy<BasicClassifier.Classifier>(() => new BasicClassifier.Classifier(ConfigurationManager.AppSettings["SentientListPath"]));
        public static double? Classify(string comment)
        {
            var classifier = ConfigurationManager.AppSettings["Classifier"];
            
            if(classifier == "none")
                return null;
            else if (classifier == "naive")
            {
                //New classifier - Should of course not be constructed here.
                var words = Tools.SplitWords(comment.ToLower()).Where(x => !string.IsNullOrEmpty(x));
                var wordsWithScore = words.Where(x => WordScores.Value.ContainsKey(x)).ToList();

                if (wordsWithScore.Count > 0)
                    return wordsWithScore.Average(x => WordScores.Value[x]);
            }

            return NielsenClassify.Value.Score(comment);
        }
    }
}
