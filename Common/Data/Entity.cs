//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Common.Data
{
    using System;
    using System.Collections.Generic;
    
    public partial class Entity
    {
        public Entity()
        {
            this.Comments = new HashSet<Comment>();
            this.Scrapees = new HashSet<Scrapee>();
            this.Posts = new HashSet<Post>();
        }
    
        public int id { get; set; }
        public string fb_id { get; set; }
        public string name { get; set; }
        public bool isPage { get; set; }
    
        public virtual ICollection<Comment> Comments { get; set; }
        public virtual ICollection<Scrapee> Scrapees { get; set; }
        public virtual ICollection<Post> Posts { get; set; }
    }
}