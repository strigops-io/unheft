using Unheft.Services;

namespace Unheft.Tests;

public class DesignerArchiverTests
{
    [Fact]
    public void Archive_RenamesFileWithArchivedExtension()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var designerPath = Path.Combine(tempDir, "Test.Designer.cs");
            File.WriteAllText(designerPath, TestFixtures.DesignerFile);

            var archivedPath = DesignerArchiver.Archive(designerPath);

            Assert.False(File.Exists(designerPath));
            Assert.True(File.Exists(archivedPath));
            Assert.EndsWith(DesignerArchiver.ArchiveExtension, archivedPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Archive_IsIdempotent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var designerPath = Path.Combine(tempDir, "Test.Designer.cs");
            File.WriteAllText(designerPath, TestFixtures.DesignerFile);

            var archivedPath1 = DesignerArchiver.Archive(designerPath);

            // Second call should not throw, even though original file is gone
            var archivedPath2 = DesignerArchiver.Archive(designerPath);

            Assert.Equal(archivedPath1, archivedPath2);
            Assert.True(File.Exists(archivedPath1));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsArchived_ReturnsTrueWhenArchived()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var designerPath = Path.Combine(tempDir, "Test.Designer.cs");
            File.WriteAllText(designerPath, TestFixtures.DesignerFile);

            Assert.False(DesignerArchiver.IsArchived(designerPath));

            DesignerArchiver.Archive(designerPath);

            Assert.True(DesignerArchiver.IsArchived(designerPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
