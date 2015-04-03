using Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FacebookDebat.Controllers
{
    public class WordController : Controller
    {
        public class Words
        {
            public string word;
            public List<Dictionary<string, object>> pages;
            public List<Dictionary<string, object>> commentators;
            public List<string> stemmedWords;
        }

        // GET: Word
        public ActionResult Page(string word)
        {
            if(LSA.WordCleaner.DK_stop == null) {
                using(var db= new Common.Data.FacebookDebatEntities()) {
                    var stopWords = db.Words.Where(x => x.stop).Select(x => x.word1).ToList();
                    LSA.WordCleaner.DK_stop = new LSA.Stoplist(new HashSet<string>(stopWords));
                }
            }

            var stemmedWord = LSA.WordCleaner.Clean(word).Single();

            var stemmedWords = DatabaseTools.ExecuteListReaderAsync(@"select w.word as cnt
                                    from Words w
                                    inner join Words sw on w.stem_id = sw.id
                                    where sw.word = @word", x => x.GetString(0), new SqlParameter("word", stemmedWord));


            var pages = DatabaseTools.ExecuteReaderAsync(@"select e.id, e.name, count(*) as cnt
                                    from Words w
                                    inner join Words sw on w.stem_id = sw.id
                                    inner join CommentWords cw on w.id = cw.word_id
                                    inner join Comments c on c.id = cw.comment_id
                                    inner join Posts p on p.id = c.post_id
                                    inner join Entities e on e.id = p.entity_id
                                    where sw.word = @wd
                                    group by e.id, e.name
                                    order by cnt desc", new SqlParameter("wd", stemmedWord));

            var commentators = DatabaseTools.ExecuteReaderAsync(@"select e.id, e.name, count(*) as cnt
                                    from Words w
                                    inner join Words sw on w.stem_id = sw.id
                                    inner join CommentWords cw on w.id = cw.word_id
                                    inner join Comments c on c.id = cw.comment_id
                                    inner join Entities e on e.id = c.entity_id
                                    where sw.word = @wd
                                    group by e.id, e.name
                                    order by cnt desc", new SqlParameter("wd", stemmedWord));

            Task.WaitAll(pages, commentators, stemmedWords);

            return View(new Words { word = word, stemmedWords = stemmedWords.Result, commentators = commentators.Result, pages = pages.Result });
        }
    }
}