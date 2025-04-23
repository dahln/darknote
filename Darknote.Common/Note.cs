namespace Darknote.Common
{
    public class Note
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public string SuggestedTitle { get; set; } = "";
        public string Content { get; set; } = "";
        public string Color { get; set; } = "";
        public bool ListEnabled { get; set; } = false;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime UpdateOn { get; set; } = DateTime.UtcNow;
        public string OwnedByEmail { get; set; }
        public bool Shared { get; set; } = false;
        public bool IsOwner { get; set; } = false;
        public int SortOrder { get; set; }
        public List<NoteListItem> NoteListItems { get; set; } = new List<NoteListItem>();
    }

    public class NoteListItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = "";
        public bool Completed { get; set; }
        public int SortOrder { get; set; }
    }

    public class SortOrderUpdate
    {
        public string Id { get; set; }
        public int NewSortOrderValue { get; set; }
    }

    public class CollaboratorsOnNote
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
    }
}

