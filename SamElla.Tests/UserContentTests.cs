using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class UserContentTests
{
  private string _root = string.Empty;
  private string _bundle = string.Empty;
  private string _user = string.Empty;

  [TestInitialize]
  public void SetUp()
  {
    _root = Path.Combine(Path.GetTempPath(), "samella-content-" + Guid.NewGuid().ToString("N"));
    _bundle = Path.Combine(_root, "bundle");
    _user = Path.Combine(_root, "user");
    Directory.CreateDirectory(Path.Combine(_bundle, "Assets"));
    Directory.CreateDirectory(Path.Combine(_bundle, "Levels"));
    File.WriteAllText(Path.Combine(_bundle, "Assets", "enemies.txt"), "original settings");
    File.WriteAllText(Path.Combine(_bundle, "Levels", "01-test.txt"), "original level");
  }

  [TestCleanup]
  public void CleanUp() => Directory.Delete(_root, true);

  [TestMethod]
  public void FirstRunCopiesContentAndLaterUpdatesOnlyUnmodifiedFiles()
  {
    UserContent.Synchronize(_bundle, _user);
    string settings = Path.Combine(_user, "Assets", "enemies.txt");
    string level = Path.Combine(_user, "Levels", "01-test.txt");
    Assert.AreEqual("original level", File.ReadAllText(level));
    File.WriteAllText(settings, "my custom settings");
    File.WriteAllText(Path.Combine(_bundle, "Assets", "enemies.txt"), "new settings");
    File.WriteAllText(Path.Combine(_bundle, "Levels", "01-test.txt"), "updated level");
    UserContent.Synchronize(_bundle, _user);
    Assert.AreEqual("my custom settings", File.ReadAllText(settings));
    Assert.AreEqual("updated level", File.ReadAllText(level));
    UserContent.Synchronize(_bundle, _user);
    Assert.AreEqual("my custom settings", File.ReadAllText(settings));
  }

  [TestMethod]
  public void CustomLevelsAndDeliberatelyDeletedDefaultsSurviveUpdates()
  {
    UserContent.Synchronize(_bundle, _user);
    string original = Path.Combine(_user, "Levels", "01-test.txt");
    File.Delete(original);
    string custom = Path.Combine(_user, "Levels", "99-my-level.txt");
    File.WriteAllText(custom, "custom map");
    File.WriteAllText(Path.Combine(_bundle, "Levels", "02-new.txt"), "new bundled map");
    UserContent.Synchronize(_bundle, _user);
    Assert.IsFalse(File.Exists(original));
    Assert.AreEqual("custom map", File.ReadAllText(custom));
    Assert.AreEqual("new bundled map", File.ReadAllText(Path.Combine(_user, "Levels", "02-new.txt")));
  }

  [TestMethod]
  public void RetiredBundledFilesAreRemovedOnlyIfUnmodified()
  {
    UserContent.Synchronize(_bundle, _user);
    string settings = Path.Combine(_user, "Assets", "enemies.txt");
    File.WriteAllText(settings, "customized retired file");
    File.Delete(Path.Combine(_bundle, "Assets", "enemies.txt"));
    File.Delete(Path.Combine(_bundle, "Levels", "01-test.txt"));
    UserContent.Synchronize(_bundle, _user);
    Assert.AreEqual("customized retired file", File.ReadAllText(settings));
    Assert.IsFalse(File.Exists(Path.Combine(_user, "Levels", "01-test.txt")));
  }
}
