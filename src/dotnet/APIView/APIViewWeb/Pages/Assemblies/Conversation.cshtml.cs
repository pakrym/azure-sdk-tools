using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using APIViewWeb.Respositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages.Assemblies
{
    public class ConversationThreadModel
    {
        public ArraySegment<LineApiView> Lines { get; set; }
        public ReviewRevisionModel Revision { get; set; }
        public CommentThreadModel Thread { get; set; }
        public bool IsOutdated { get; set; }
    }

    public class ConversationPageModel : PageModel
    {
        private readonly CommentsManager _commentsManager;

        private readonly ReviewManager _manager;

        private readonly BlobCodeFileRepository _codeFileRepository;

        public ConversationPageModel(
            CommentsManager commentsManager,
            ReviewManager manager,
            BlobCodeFileRepository codeFileRepository)
        {
            _commentsManager = commentsManager;
            _manager = manager;
            _codeFileRepository = codeFileRepository;
        }

        public ReviewModel Review { get; set; }
        public ConversationThreadModel[] Threads { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            TempData["Page"] = "conversation";

            Review = await _manager.GetReviewAsync(User, id);
            var comments = await _commentsManager.GetReviewCommentsAsync(id);

            List<ConversationThreadModel> conversationThreads = new List<ConversationThreadModel>();

            foreach (var commentThreadModel in comments.Threads)
            {
                if (commentThreadModel.LineId != null)
                {
                    conversationThreads.Add(new ConversationThreadModel()
                    {
                        Thread = commentThreadModel
                    });
                }
            }

            for (var index = 0; index < Review.Revisions.Count; index++)
            {
                var latestRevision = index == Review.Revisions.Count - 1;

                var revision = Review.Revisions[index];
                var codeFile = await _codeFileRepository.GetCodeFileAsync(revision.RevisionId, revision.Files.Single().ReviewFileId);
                var lines = new CodeFileHtmlRenderer().Render(codeFile).ToArray();
                Dictionary<string, int> lineIds = new Dictionary<string, int>();
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].ElementId != null)
                    {
                        lineIds[lines[i].ElementId] = i;
                    }
                }

                foreach (var conversationThread in conversationThreads)
                {
                    if (conversationThread.Thread.ReviewId == revision.RevisionId ||
                        conversationThread.Thread.ReviewId == null)
                    {
                        conversationThread.Revision = revision;
                    }

                    if (lineIds.TryGetValue(conversationThread.Thread.LineId, out int lineIndex))
                    {
                        var min = Math.Max(0, lineIndex - 2);

                        conversationThread.Lines = new ArraySegment<LineApiView>(lines, min, lineIndex - min + 1);
                    }
                    else
                    {
                        conversationThread.Lines = new ArraySegment<LineApiView>(Array.Empty<LineApiView>(), 0, 0);
                    }

                    if (latestRevision && !lineIds.TryGetValue(conversationThread.Thread.LineId, out _))
                    {
                        conversationThread.IsOutdated = true;
                    }
                }
            }
            Threads = conversationThreads.ToArray();
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync(string id, [FromForm] IFormFile upload)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage();
            }

            if (upload != null)
            {
                var openReadStream = upload.OpenReadStream();
                await _manager.AddRevisionAsync(User, id, upload.FileName, openReadStream);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id, string revisionId)
        {
            await _manager.DeleteRevisionAsync(User, id, revisionId);

            return RedirectToPage();
        }
    }
}
