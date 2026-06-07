using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Queries.GetNote;

public sealed record GetNoteQuery(Guid Id) : IRequest<NoteDto>;
