# EpubWriter
Epub writer is a lightweight library for creation of valid Epub version 3 files.

## Usage

```C#
string language = "en-us";
string title = "My First Ebook";
string author = "me";
using (Epub epub = new Epub(language, title, author))
{
  //Gets the content of a xhtml file
  string someXhtmlFile = File.ReadAllText("myXhtmlFile.xhtml");
  
  //Adding a chapter to the ebook.
  //Adds to the navigation. Files are displayed in the order they're added
  epub.AddSpine(
  	//Adds the file to the manifest and to the zip package
	epub.AddTextFile("zipFileName.xhtml", someXhtmlFile, Epub.TextMediaType.Xhtml),
	"Navigation Title";
	);
  //If our xhtml contains a image, we must added it to the zip package,
  epub.AddImage("Images/myImage.png", File.ReadAllBytes("myImage.png"));
  
  //Adds a cover image to the book
  epub.AddImage("cover.png", File.ReadAllBytes("myCover.png"), true); 
  
  epub.Save("OutputFile.epub");
}
```
