using System.Drawing;
using System.Windows.Forms;

namespace DatabaseManager.Helper
{
    public static class ImageListHelper
    {
        public static void LoadImagesFromResources(ImageList imageList, string[] imageNames)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var baseName = "DatabaseManager.Resources.";

            imageList.Images.Clear();
            
            foreach (var name in imageNames)
            {
                try
                {
                    var resourceName = baseName + name;
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        imageList.Images.Add(Image.FromStream(stream));
                    }
                }
                catch
                {
                }
            }
        }

        public static void SetupTreeViewImageList(TreeView treeView, ImageList imageList)
        {
            treeView.ImageList = imageList;
            treeView.ImageIndex = 0;
            treeView.SelectedImageIndex = 0;
        }
    }
}