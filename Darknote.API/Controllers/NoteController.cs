using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Darknote.API.Utility;
using Darknote.Database;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Darknote.APi.Controllers
{
    public class NoteController : Controller
    {
        private ApplicationDbContext _db;
        public NoteController(ApplicationDbContext dbContext)
        {
            _db = dbContext;
        }

        [Authorize]
        [HttpPost]
        [Route("api/v1/note")]
        async public Task<IActionResult> NoteCreate([FromBody] Common.Note model)
        {
            string userId = User.GetUserId();

            if(string.IsNullOrEmpty(model.Title) == false && model.Title.Length > 1000)
            {
                return BadRequest("Title must be less than 1,000 characters");
            }
            if(string.IsNullOrEmpty(model.Content) == false && model.Content.Length > 2000000)
            {
                return BadRequest("Content must be less than 2,000,000 characters");
            }

            Database.Note note = new Database.Note()
            {
                Title = model.Title,
                Content = model.Content,
                OwnerId = userId,
                Color = model.Color,
                ListEnabled = model.ListEnabled,
                SortOrder = await GetNextSortOrder(userId)
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            return Ok(note.Id);
        }

        [Authorize]
        [HttpGet]
        [Route("api/v1/note/{noteId}")]
        async public Task<IActionResult> NoteGetById(string noteId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, false);
            if(!isAuthorized)
                return BadRequest();

            var note = await _db.Notes.Include(x => x.NoteListItems)
                                    .Include(x => x.NoteAuthorizedUsers)
                                    .Where(c => c.Id == noteId).FirstOrDefaultAsync();
            if (note == null)
                return BadRequest("Note not found");

            string ownerByEmail = string.Empty;
            if(note.OwnerId != userId)
                ownerByEmail = await _db.Users.Where(x => x.Id == note.OwnerId).Select(x => x.Email ).FirstOrDefaultAsync();


            var response = new Common.Note()
            {
                Id = note.Id,
                Title = note.Title,
                Content = note.Content,
                Color = note.Color,
                ListEnabled = note.ListEnabled,
                Shared = note.NoteAuthorizedUsers.Any(),
                IsOwner = note.OwnerId == userId,
                OwnedByEmail = note.OwnerId != userId ? ownerByEmail : string.Empty,
                NoteListItems = note.NoteListItems.Select(x => new Common.NoteListItem() {
                    Id = x.Id,
                    Completed = x.Completed,
                    Content = x.Content,
                    SortOrder = x.SortOrder
                }).OrderBy(x => x.SortOrder).ToList()
            };

            return Ok(response);
        }

        [Authorize]
        [HttpPut]
        [Route("api/v1/note/{noteId}")]
        async public Task<IActionResult> NoteUpdateById([FromBody] Common.Note model, string noteId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, false);
            if(!isAuthorized)
                return BadRequest();

            var note = await _db.Notes.Include(x => x.NoteListItems).Where(c => c.Id == noteId).FirstOrDefaultAsync();
            if (note == null)
            {
                return BadRequest("Note not found");
            }
            if(string.IsNullOrEmpty(model.Title) == false && model.Title.Length > 1000)
            {
                return BadRequest("Title must be less than 1,000 characters");
            }
            if(string.IsNullOrEmpty(model.Content) == false && model.Content.Length > 2000000)
            {
                return BadRequest("Content must be less than 2,000,000 characters");
            }

            note.Title = model.Title;
            note.Content = model.Content;
            note.ListEnabled = model.ListEnabled;
            note.Color = model.Color;
            note.UpdateOn = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(note.Id);
        }

        [Authorize]
        [HttpDelete]
        [Route("api/v1/note/{noteId}")]
        async public Task<IActionResult> NoteDeleteById(string noteId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, true);
            if(!isAuthorized)
                return BadRequest();

            var note = await _db.Notes.Where(c => c.OwnerId == userId && c.Id == noteId).FirstOrDefaultAsync();

            if(note == null)
            {
                return BadRequest("Note not found");
            }

            var shares = _db.NoteAuthorizedUsers.Where(x => x.NoteId == noteId);
            _db.NoteAuthorizedUsers.RemoveRange(shares);

            var items = _db.NoteListItems.Where(x => x.NoteId == noteId);
            _db.NoteListItems.RemoveRange(items);

            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();
            return Ok();
        }


        [Authorize]
        [HttpPost]
        [Route("api/v1/notes")]
        async public Task<IActionResult> NoteSearch([FromBody] Common.Search model)
        {
            string userId = User.GetUserId();

            var query = _db.Notes.AsNoTracking()
                            .Include(x => x.NoteListItems)
                            .Include(x => x.NoteAuthorizedUsers)
                            .Where(c => c.OwnerId == userId);

            if (!string.IsNullOrEmpty(model.FilterText))
            {
                query = query.Where(i => i.Title.ToLower().Contains(model.FilterText.ToLower()) ||
                                        i.Content.ToLower().ToLower().Contains(model.FilterText.ToLower()) ||
                                        i.NoteListItems.Any(x => x.Content.ToLower().Contains(model.FilterText.ToLower()))
                                    );
            }

            query = query.OrderByDescending(c => c.SortOrder);

            Common.SearchResponse<Common.Note> response = new Common.SearchResponse<Common.Note>();
            response.Total = await query.CountAsync();

            var dataResponse = await query.Skip(model.Page * model.PageSize)
                                        .Take(model.PageSize)
                                        .ToListAsync();

            response.Results = dataResponse.Select(x => new Common.Note()
            {
                Id = x.Id,
                Title = x.Title,
                SuggestedTitle = GenerateSuggestedTitle(x),
                Color = x.Color,
                UpdateOn = x.UpdateOn,
                SortOrder = x.SortOrder,
                ListEnabled = x.ListEnabled,
                IsOwner = true,
                Shared = x.NoteAuthorizedUsers.Any(),
                NoteListItems = x.NoteListItems.Take(1).Select(x => new Common.NoteListItem() {
                    Completed = x.Completed,
                    Content = x.Content
                }).ToList()
            }).ToList();

            //Get collab notes
            var lastPage = ((model.Page + 1) < (Math.Ceiling((double)response.Total / model.PageSize))) == false;
            var maxSort = response.Results.Select(x => x.SortOrder).DefaultIfEmpty(0).Max();
            var minSort = response.Results.Select(x => x.SortOrder).DefaultIfEmpty(0).Min();
            var collaboratorNotes = await _db.NoteAuthorizedUsers.Where(x => x.UserId == userId && (lastPage == false ? x.SortOrder > minSort : true) && (model.Page != 0 ? x.SortOrder < maxSort : true)).ToListAsync();
            var collaboratorNotesActual = await _db.Notes.Where(x => collaboratorNotes.Select(c => c.NoteId).Contains(x.Id)).ToListAsync();
            foreach(var col in collaboratorNotesActual)
            {
                col.SortOrder = collaboratorNotes.FirstOrDefault(x => x.NoteId == col.Id).SortOrder;
            }
            var collabResult = collaboratorNotesActual.Select(x => new Common.Note()
            {
                Id = x.Id,
                Title = x.Title,
                SuggestedTitle = GenerateSuggestedTitle(x),
                Color = x.Color,
                UpdateOn = x.UpdateOn,
                SortOrder = x.SortOrder,
                ListEnabled = x.ListEnabled,
                Shared = true,
                IsOwner = false,
                NoteListItems = x.NoteListItems.Take(1).Select(x => new Common.NoteListItem() {
                    Completed = x.Completed,
                    Content = x.Content
                }).ToList()
            }).ToList();
            response.Results.AddRange(collabResult);
            response.Results = response.Results.OrderByDescending(x => x.SortOrder).ToList();

            return Ok(response);
        }

        [Authorize]
        [HttpPost]
        [Route("api/v1/notes/sort")]
        async public Task<IActionResult> NoteSort([FromBody] List<Common.SortOrderUpdate> content)
        {
            string userId = User.GetUserId();

            var noteIdsToUpdate = content.Select(x => x.Id).ToList();
            var notesToUpdate = await _db.Notes.Where(x => x.OwnerId == userId && noteIdsToUpdate.Contains(x.Id)).ToListAsync();
            var notesSharedToUpdate = await _db.NoteAuthorizedUsers.Where(x => x.UserId == userId && noteIdsToUpdate.Contains(x.NoteId)).ToListAsync();
            foreach(var note in notesToUpdate)
            {
                note.SortOrder = content.FirstOrDefault(x => x.Id == note.Id).NewSortOrderValue;
            }
            foreach(var note in notesSharedToUpdate)
            {
                note.SortOrder = content.FirstOrDefault(x => x.Id == note.NoteId).NewSortOrderValue;
            }
            await _db.SaveChangesAsync();

            //Reconcile SortOrder for ALL notes (owned or shared) for this user
            var userNotes = await _db.Notes.Where(x => x.OwnerId == userId).ToListAsync();
            var userNotesSharedByOthers = await _db.NoteAuthorizedUsers.Where(x => x.UserId == userId).ToListAsync();
            List<Common.SortOrderUpdate> notes = new List<Common.SortOrderUpdate>();
            notes.AddRange(userNotes.Select(x => new Common.SortOrderUpdate()
            {
                Id = x.Id,
                NewSortOrderValue = x.SortOrder
            }));
            notes.AddRange(userNotesSharedByOthers.Select(x => new Common.SortOrderUpdate()
            {
                Id = x.Id,
                NewSortOrderValue = x.SortOrder
            }));
            int reconciledSortValue = notes.Count();
            foreach(var note in notes.OrderByDescending(x => x.NewSortOrderValue))
            {
                var noteToUpdate = await _db.Notes.FirstOrDefaultAsync(x => x.OwnerId == userId && x.Id == note.Id);
                var shareToUpdate = await _db.NoteAuthorizedUsers.FirstOrDefaultAsync(x => x.UserId == userId && x.NoteId == note.Id);
                if(noteToUpdate != null)
                    noteToUpdate.SortOrder = reconciledSortValue--;
                else if(shareToUpdate != null)
                    shareToUpdate.SortOrder = reconciledSortValue--;
            }
            await _db.SaveChangesAsync();

            return Ok();
        }

        [Authorize]
        [HttpPost]
        [Route("api/v1/items/sort")]
        async public Task<IActionResult> ItemSort([FromBody] List<Common.SortOrderUpdate> content)
        {
            string userId = User.GetUserId();
            
            var itemsIdsToUpdate = content.Select(x => x.Id).ToList();
            var itemsToUpdate = _db.NoteListItems.Where(x => itemsIdsToUpdate.Contains(x.Id)).ToList();
            
            var notesIds = itemsToUpdate.Select(x => x.NoteId).Distinct();
            if(notesIds.Count() > 1)
                return BadRequest("Error: More than 1 NoteId."); //There shouldn't be more than 1 id. If there is more than 1, then there was a problem.
            var isAuthorized = await IsAuthorizedForNote(userId, notesIds.FirstOrDefault(), false);
            if(!isAuthorized)
                return BadRequest();
            
            foreach(var item in itemsToUpdate)
            {
                item.SortOrder = content.FirstOrDefault(x => x.Id == item.Id).NewSortOrderValue;
            }
            await _db.SaveChangesAsync();

            return Ok();
        }


        [Authorize]
        [HttpPut]
        [Route("api/v1/note/{noteId}/item/{itemId}")]
        async public Task<IActionResult> NoteUpdateItemUpdateById([FromBody] Common.NoteListItem model, string noteId, string itemId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, false);
            if(!isAuthorized)
                return BadRequest();

            var note = await _db.Notes.Include(x => x.NoteListItems).Where(c => c.Id == noteId).FirstOrDefaultAsync();
            if (note == null)
            {
                return BadRequest("Note not found");
            }

            if(string.IsNullOrEmpty(model.Content) == false && model.Content.Length > 1000)
            {
                return BadRequest("Item must be less than 1000 characters");
            }

            var item = note.NoteListItems.FirstOrDefault(x => x.Id == itemId);
            if (item == null)
            {
                

                var newSortOrder = 0;
                var anyNote = await _db.NoteListItems.AnyAsync();
                if(anyNote)
                {
                    newSortOrder = await _db.NoteListItems.MaxAsync(x => x.SortOrder);
                    newSortOrder++;
                }

                var newNoteListItem = new NoteListItem()
                {
                    Id = model.Id,
                    NoteId = noteId,
                    Content = model.Content,
                    Completed = model.Completed,
                    SortOrder = newSortOrder
                };
                model.SortOrder = newSortOrder;

                _db.NoteListItems.Add(newNoteListItem);
                await _db.SaveChangesAsync();
            }
            else
            {
                model.Id = item.Id;
                item.Completed = model.Completed;
                item.Content = model.Content;
                await _db.SaveChangesAsync();
            }


            return Ok(model);
        }

        [Authorize]
        [HttpDelete]
        [Route("api/v1/note/{noteId}/item/{itemId}")]
        async public Task<IActionResult> NoteUpdateItemDeleteById(string noteId, string itemId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, false);
            if(!isAuthorized)
                return BadRequest();

            var note = await _db.Notes.Include(x => x.NoteListItems).Where(c => c.Id == noteId).FirstOrDefaultAsync();
            if (note == null)
            {
                return BadRequest("Note not found");
            }

            var item = note.NoteListItems.FirstOrDefault(x => x.Id == itemId);
            if(item == null)
            {
                return BadRequest("Item not found");
            }

            _db.NoteListItems.Remove(item);
            await _db.SaveChangesAsync();

            return Ok(note.Id);
        }

        [Authorize]
        [HttpDelete]
        [Route("api/v1/note/{noteId}/completed")]
        async public Task<IActionResult> NoteUpdateItemsCompletedDeleteById(string noteId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, false);
            if(!isAuthorized)
                return BadRequest();

            var note = await _db.Notes.Include(x => x.NoteListItems).Where(c => c.Id == noteId).FirstOrDefaultAsync();
            if (note == null)
            {
                return BadRequest("Note not found");
            }

            var items = note.NoteListItems.Where(x => x.Completed == true);

            _db.NoteListItems.RemoveRange(items);
            await _db.SaveChangesAsync();

            return Ok(note.Id);
        }

        [Authorize]
        [HttpPost]
        [Route("api/v1/note/{noteId}/share")]
        public async Task<IActionResult> AddCollaborator([FromBody] Common.CollaboratorsOnNote model, string noteId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, true);
            if(!isAuthorized)
                return BadRequest();

            if(string.IsNullOrEmpty(model.Email))
                return BadRequest("User email is required");

            var userInfo = await _db.Users.Where(x => x.Email.ToLower() == model.Email.ToLower()).Select(x => new { x.Id, x.Email }).FirstOrDefaultAsync();
            if (userInfo == null)
                return BadRequest("User not found");

            //Get the sort order for the collab user. Ensure the new shared note is added to the top of their list.
            var newSortOrder = 0;
            var anyNote = await _db.Notes.Where(x => x.OwnerId == userInfo.Id).AnyAsync();
            if(anyNote)
            {
                newSortOrder = await _db.Notes.Where(x => x.OwnerId == userInfo.Id).MaxAsync(x => x.SortOrder);
                newSortOrder++;
            }

            NoteAuthorizedUser noteAuthorizedUser = new NoteAuthorizedUser()
            {
                UserId = userInfo.Id,
                NoteId = noteId,
                SortOrder = await GetNextSortOrder(userInfo.Id)
            };
            _db.NoteAuthorizedUsers.Add(noteAuthorizedUser);
            await _db.SaveChangesAsync();
            
            return Ok(new Common.CollaboratorsOnNote()
            {
                Id = noteAuthorizedUser.Id,
                UserId = userInfo.Id,
                Email = userInfo.Email
            });
        }

        [Authorize]
        [HttpDelete]
        [Route("api/v1/note/{noteId}/share/{shareId}")]
        public async Task<IActionResult> RemoveCollaborator(string noteId, string shareId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, true);
            if(!isAuthorized)
                return BadRequest();

            var collaborator = _db.NoteAuthorizedUsers.Where(x => x.NoteId == noteId && x.Id == shareId);
            _db.NoteAuthorizedUsers.RemoveRange(collaborator);
            await _db.SaveChangesAsync();

            return Ok();
        }

        [Authorize]
        [HttpDelete]
        [Route("api/v1/note/{noteId}/user")]
        public async Task<IActionResult> RemoveSelfAsCollaborator(string noteId)
        {
            //The user is not the owner. They want to leave a 'shared' note.
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, false);
            if(!isAuthorized)
                return BadRequest();

            var collaborator = _db.NoteAuthorizedUsers.Where(x => x.NoteId == noteId && x.UserId == userId);
            _db.NoteAuthorizedUsers.RemoveRange(collaborator);
            await _db.SaveChangesAsync();

            return Ok();
        }

        [Authorize]
        [HttpGet]
        [Route("api/v1/note/{noteId}/shares")]
        public async Task<IActionResult> GetCollaboratorsForNote(string noteId)
        {
            string userId = User.GetUserId();
            var isAuthorized = await IsAuthorizedForNote(userId, noteId, true);
            if(!isAuthorized)
                return BadRequest();

            var collaborators = await _db.NoteAuthorizedUsers.AsNoTracking()
                                    .Where(x => x.NoteId == noteId)
                                    .Select(x => new Common.CollaboratorsOnNote()
                                    {
                                        Id = x.Id,
                                        UserId = x.UserId
                                    })
                                    .ToListAsync();

            foreach(var user in collaborators)
            {
                user.Email = await _db.Users.Where(x => x.Id == user.UserId).Select(x => x.Email).FirstOrDefaultAsync();
            }
            
            return Ok(collaborators);
        }

        private async Task<bool> IsAuthorizedForNote(string userId, string noteId, bool isOwner = false)
        {
            var isOwnerResult = await _db.Notes.AsNoTracking().AnyAsync(x => x.Id == noteId && x.OwnerId == userId);
            if(isOwner)
            {
                return isOwnerResult;
            }
            else if(isOwnerResult)
            {
                return isOwnerResult;
            }
            else
            {
                var isAuthorized = await _db.NoteAuthorizedUsers.AsNoTracking().AnyAsync(x => x.NoteId == noteId && x.UserId == userId);
                return isAuthorized;
            }
        }

        private string GenerateSuggestedTitle(Darknote.Database.Note note)
        {
            int wordCountToReturn = 4;

            string title = "[NO TITLE]";
            if(string.IsNullOrEmpty(note.Title) == false)
                title = note.Title;
            else if(string.IsNullOrEmpty(note.Content) == false && note.ListEnabled == false)
                title = string.Join(" ", note.Content.Split(' ','\n').Take(wordCountToReturn)) + (note.Content.Split(' ','\n').Count() > wordCountToReturn ? "..." : "");
            else if(note.NoteListItems.Any() && note.ListEnabled == true)
                title = string.Join(" ", note.NoteListItems.Where(x => string.IsNullOrEmpty(x.Content) == false).FirstOrDefault().Content.Split(' ','\n').Take(wordCountToReturn)) + (note.NoteListItems.Where(x => string.IsNullOrEmpty(x.Content) == false).FirstOrDefault().Content.Count() > wordCountToReturn ? "..." : "");

            return title;
        }

        private async Task<int> GetNextSortOrder(string userId)
        {
            int maxSortOrderFromOwned = 0;
            int maxSortOrderFromShares = 0;
            var anyNotes = await _db.Notes.AnyAsync(x => x.OwnerId == userId);
            var anyShares = await _db.NoteAuthorizedUsers.AnyAsync(x => x.UserId == userId);
            
            if(anyNotes)
            {
                maxSortOrderFromOwned = await _db.Notes.Where(x => x.OwnerId == userId).MaxAsync(x => x.SortOrder);
            }

            if(anyShares)
            {
                maxSortOrderFromShares = await _db.NoteAuthorizedUsers.Where(x => x.UserId == userId).MaxAsync(x => x.SortOrder);
            }

            if(maxSortOrderFromOwned == 0 && maxSortOrderFromShares == 0)
                return 0;
            if(maxSortOrderFromOwned > maxSortOrderFromShares)
                return maxSortOrderFromOwned + 1;
            else
                return maxSortOrderFromShares + 1;
        }
    }
}


