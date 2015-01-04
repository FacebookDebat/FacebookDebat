using Common.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FacebookDebat.Models
{
    public class PoliticianModel
    {
        public Scrapee Scrapee;
        public Entity Entity;
        public List<PPost> Posts;
        public List<PWord> Words;
        public List<Commenter> Commenters;

        public class PPost
        {
            public Post Post;
            public int Count;
        }

        public class PWord
        {
            public Word Word;
            public int Count;
        }

        public class Commenter
        {
            public int id;
            public string name;
            public int Count;
        }
    }

}