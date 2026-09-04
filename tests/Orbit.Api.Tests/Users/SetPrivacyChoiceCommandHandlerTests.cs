using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Core.Users.SetPrivacyChoice;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// The footer's "Do not share my personal information". On the account rather than in the browser
/// because it is a standing instruction: somebody who has said it once should not have to say it again
/// on their phone.
/// </summary>
public sealed class SetPrivacyChoiceCommandHandlerTests
{
    [Fact]
    public async Task An_account_shares_with_third_parties_until_it_says_otherwise()
    {
        var users = new InMemoryUserRepository();
        var user = User.Create("a@example.com", "anna", "Anna", "hash");
        await users.AddAsync(user, CancellationToken.None);

        Assert.False((await users.GetByIdAsync(user.Id, CancellationToken.None))!.KeepsThirdPartiesOut);
    }

    [Fact]
    public async Task Saying_it_is_remembered_on_the_account()
    {
        var users = new InMemoryUserRepository();
        var user = User.Create("a@example.com", "anna", "Anna", "hash");
        await users.AddAsync(user, CancellationToken.None);
        var handler = new SetPrivacyChoiceCommandHandler(users);

        var answered = await handler.HandleAsync(
            new SetPrivacyChoiceCommand(user.Id, KeepsThirdPartiesOut: true), CancellationToken.None);

        Assert.True(answered);
        Assert.True((await users.GetByIdAsync(user.Id, CancellationToken.None))!.KeepsThirdPartiesOut);
    }

    [Fact]
    public async Task It_can_be_taken_back()
    {
        var users = new InMemoryUserRepository();
        var user = User.Create("a@example.com", "anna", "Anna", "hash");
        await users.AddAsync(user, CancellationToken.None);
        var handler = new SetPrivacyChoiceCommandHandler(users);
        await handler.HandleAsync(new SetPrivacyChoiceCommand(user.Id, true), CancellationToken.None);

        await handler.HandleAsync(new SetPrivacyChoiceCommand(user.Id, false), CancellationToken.None);

        Assert.False((await users.GetByIdAsync(user.Id, CancellationToken.None))!.KeepsThirdPartiesOut);
    }

    [Fact]
    public async Task An_account_that_is_not_there_answers_no()
    {
        var handler = new SetPrivacyChoiceCommandHandler(new InMemoryUserRepository());

        var answered = await handler.HandleAsync(
            new SetPrivacyChoiceCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.False(answered);
    }
}
