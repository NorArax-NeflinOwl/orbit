using Orbit.Core.Abstractions;
using Orbit.Core.Notes.GetNotePicture;

namespace Orbit.Core.Sharing.GetPublicSharedPicture;

/// <summary>
/// A picture of a note somebody was sent a link to. Only an unsealed one: a sealed note cannot be linked
/// at all (PublicSharedItemReader refuses it), so a sealed picture behind a link is a contradiction and
/// is answered as nothing.
/// </summary>
public sealed record GetPublicSharedPictureQuery(string Token, Guid PictureId) : IRequest<NotePictureContent?>;
