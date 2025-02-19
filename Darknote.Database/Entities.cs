using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Darknote.Database
{
    public class NoteAuthorizedUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public int SortOrder { get; set; }

        public string NoteId { get; set; }
        public Note Note { get; set; } 
    }
    public class Note
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [MaxLength(1000)]
        public string Title { get; set; }
        [MaxLength(2000000)]
        public string Content { get; set; }
        public string Color { get; set; }
        public bool ListEnabled { get; set; } = false;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime UpdateOn { get; set; } = DateTime.UtcNow;
        public string OwnerId { get; set; }
        public int SortOrder { get; set; }

        public List<NoteListItem> NoteListItems { get; set; } = new List<NoteListItem>();
        public List<NoteAuthorizedUser> NoteAuthorizedUsers { get; set; } = new List<NoteAuthorizedUser>();
    }

    public class NoteListItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [MaxLength(1000)]
        public string Content { get; set; }
        public bool Completed { get; set; }
        public int SortOrder { get; set; }

        public string NoteId { get; set; }
    }

    public class SystemSetting
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? SendGridKey { get; set; }
        public string? SendGridSystemEmailAddress { get; set; }
        public bool RegistrationEnabled { get; set; } = true;
        public string? EmailDomainRestriction { get; set; }
    }
}


