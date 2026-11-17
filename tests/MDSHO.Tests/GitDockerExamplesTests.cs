using System.Linq;
using MDSHO.Core;
using Xunit;

namespace MDSHO.Tests {

  /// <summary>
  /// The Git and Docker groups of the Run command Examples (1.14.2): the command is the title, each runs in Command
  /// Prompt with the window kept open, and the ones that work on a repository fill in a working folder to replace.
  /// </summary>
  public class GitDockerExamplesTests {

    [Theory]
    [InlineData("Git", "git clone https://...", "git clone https://github.com/MyName/MyRepo.git", "C:\\Repos", "Downloads a copy of a repository into a new folder; replace the address with the repository's.")]
    [InlineData("Git", "git remote", "git remote", "C:\\Repos\\MyRepo", "Lists the remote repositories this one is linked to.")]
    [InlineData("Git", "git remote show origin", "git remote show origin", "C:\\Repos\\MyRepo", "Shows the address of origin and which branches it tracks.")]
    [InlineData("Git", "git fetch", "git fetch", "C:\\Repos\\MyRepo", "Downloads the latest changes from the remote without changing your files.")]
    [InlineData("Git", "git checkout --track origin/BranchX", "git checkout --track origin/BranchX", "C:\\Repos\\MyRepo", "Creates a local branch that follows the remote branch BranchX and switches to it.")]
    [InlineData("Git", "git branch -l", "git branch -l", "C:\\Repos\\MyRepo", "Lists the local branches; the current one is marked with *.")]
    [InlineData("Git", "git checkout main", "git checkout main", "C:\\Repos\\MyRepo", "Switches to the main branch.")]
    [InlineData("Git", "git pull", "git pull", "C:\\Repos\\MyRepo", "Downloads the latest changes and merges them into the current branch.")]
    [InlineData("Git", "git status", "git status", "C:\\Repos\\MyRepo", "Shows which files are changed, staged or new.")]
    [InlineData("Git", "git diff", "git diff", "C:\\Repos\\MyRepo", "Shows the changes not staged yet, line by line.")]
    [InlineData("Git", "git log --oneline -20", "git log --oneline -20", "C:\\Repos\\MyRepo", "Shows the last 20 commits, one line each.")]
    [InlineData("Git", "git branch LocalWorkingBranch", "git branch LocalWorkingBranch", "C:\\Repos\\MyRepo", "Creates the branch LocalWorkingBranch without switching to it.")]
    [InlineData("Git", "git checkout LocalWorkingBranch", "git checkout LocalWorkingBranch", "C:\\Repos\\MyRepo", "Switches to the branch LocalWorkingBranch.")]
    [InlineData("Git", "git switch -c NewBranch", "git switch -c NewBranch", "C:\\Repos\\MyRepo", "Creates the branch NewBranch and switches to it.")]
    [InlineData("Git", "git stash", "git stash", "C:\\Repos\\MyRepo", "Puts your uncommitted changes aside, leaving the files as in the last commit.")]
    [InlineData("Git", "git stash pop", "git stash pop", "C:\\Repos\\MyRepo", "Brings back the changes last put aside with git stash.")]
    [InlineData("Git", "git branch -d LocalWorkingBranch", "git branch -d LocalWorkingBranch", "C:\\Repos\\MyRepo", "Deletes the branch LocalWorkingBranch, only if it's fully merged.")]
    [InlineData("Git", "git branch -D LocalWorkingBranch", "git branch -D LocalWorkingBranch", "C:\\Repos\\MyRepo", "Deletes the branch LocalWorkingBranch even if it has changes that aren't merged.")]
    [InlineData("Docker", "docker ps", "docker ps", "", "Lists the running containers.")]
    [InlineData("Docker", "docker ps -a", "docker ps -a", "", "Lists all containers, stopped ones too.")]
    [InlineData("Docker", "docker images", "docker images", "", "Lists the images on this PC.")]
    [InlineData("Docker", "docker start MyContainer", "docker start MyContainer", "", "Starts the stopped container MyContainer.")]
    [InlineData("Docker", "docker stop MyContainer", "docker stop MyContainer", "", "Stops the running container MyContainer.")]
    [InlineData("Docker", "docker logs MyContainer", "docker logs MyContainer", "", "Shows what the container MyContainer has written to its log.")]
    [InlineData("Docker", "docker compose up -d", "docker compose up -d", "C:\\Repos\\MyRepo", "Starts the services of the folder's compose file in the background.")]
    [InlineData("Docker", "docker compose down", "docker compose down", "C:\\Repos\\MyRepo", "Stops and removes the services started with docker compose up.")]
    public void Each_git_and_docker_example_has_the_agreed_values(string category, string name, string arguments, string folder, string description) {
      RunCommandExample e = Assert.Single(RunCommandExamples.All, x => x.Name == name);
      Assert.Equal(category, e.Category);
      Assert.Equal("cmd.exe", e.ProgramFileName);
      Assert.Equal(arguments, e.Arguments);
      Assert.Equal(folder, e.WorkingFolder);
      Assert.True(e.KeepWindowOpen);
      Assert.False(e.RunAsAdministrator);
      Assert.Equal(description, e.Description);
      Assert.DoesNotContain("\n", e.Description);
    }

    [Fact]
    public void There_are_18_git_and_8_docker_examples() {
      Assert.Equal(18, RunCommandExamples.All.Count(e => e.Category == "Git"));
      Assert.Equal(8, RunCommandExamples.All.Count(e => e.Category == "Docker"));
    }

    [Fact]
    public void The_tooltip_names_the_working_folder_on_a_second_line() {
      RunCommandExample status = RunCommandExamples.All.Single(x => x.Name == "git status");
      Assert.Equal("Runs: cmd.exe /K git status\nIn: C:\\Repos\\MyRepo", status.RunsText);
      RunCommandExample ps = RunCommandExamples.All.Single(x => x.Name == "docker ps");
      Assert.Equal("Runs: cmd.exe /K docker ps", ps.RunsText);
    }
  }
}
