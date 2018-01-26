using System;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using System.IO.Compression;

namespace EpubWriter
{
	/// <summary>
	/// Class for creating an Epub 3 document
	/// </summary>
	public class Epub : IDisposable
	{
        #region Paths

        private string MetaFolder => "META-INF";
		public string ContentFolder => "OEBPS";
        
        #endregion //Paths

        #region Namespaces

        private readonly XNamespace opfNamespace = @"http://www.idpf.org/2007/opf";
		private readonly XNamespace dcNamespace = @"http://purl.org/dc/elements/1.1/";

        #endregion //Namespaces

        #region Private variables

        private MemoryStream zipStream;
        private ZipArchive zip;
        private Navigation nav;
        private XDocument opf;		
        private XElement spine;
        private XElement manifest;
        private XElement metadata;

        #endregion //Private variables

        #region Enums

        public enum TextMediaType { Css, Xhtml, Javascript};
        public enum NavigationPosition { Hidden, FirstChapter };

        #endregion //Enums

        #region Constructors and initializers

        /// <summary>
        /// Creates a new instance if the Epub class
        /// </summary>
        /// <param name="fileName">Path to the output file</param>
        /// <param name="language">Language of the book, in the format "en" or "en-us"</param>
        /// <param name="title">Title of the book</param>
        /// <param name="author">Main author of the book</param>
        public Epub(string language, string title, string author=null)
        {
            //Creates a new zip file in memory
            zipStream = new MemoryStream();
            CreateMimetype();

            zip = new ZipArchive(zipStream, ZipArchiveMode.Update, true);

            InitializeOPF(language, title, author);

            nav = new Navigation();
        }

        /// <summary>
        /// Creates the mimetype file
        /// </summary>
        private void CreateMimetype()
        {
            //According to the specification, the mimetype must be the first file in the package 
            //and cannot be compressed. ZipArchieve class don't have the option to set the compression
            //mode, only the compression level. So, even if we set the compression level to NoCompression
            //the file will still have the compression mode set to deflate and additional bytes 
            //will be added before the file content. Because of this we're using Jaime Olivares's ZipStorer
            //class, so we can create a truely uncompressed mimetype file
            using (ZipStorer zs = ZipStorer.Create(zipStream, "", true))
            using (MemoryStream mt = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(mt);
                
                sw.Write("application/epub+zip");
                sw.Flush();
                mt.Seek(0, SeekOrigin.Begin);
                zs.AddStream(ZipStorer.Compression.Store, "mimetype", mt, DateTime.Now, "");
            }
            
        }

        /// <summary>
        /// Initialize the OPF file
        /// </summary>
        /// <param name="language">Language</param>
        /// <param name="title">Title</param>
        /// <param name="author">Author</param>
        private void InitializeOPF(string language, string title, string author = null)
		{
			opf = new XDocument(new XDeclaration("1.0", "utf-8", null));
			
			//Creates the root element
			opf.Add(new XElement(opfNamespace + "package",
						new XAttribute("version", "3.0"),
						new XAttribute(XNamespace.Xml + "lang", language),
						new XAttribute("unique-identifier","pub-id")
						)
					);

			//Creates the metadata element
			metadata =
				new XElement(opfNamespace + "metadata",
					new XAttribute(XNamespace.Xmlns + "dc", dcNamespace),
					new XElement(dcNamespace + "identifier",
						new XAttribute("id", "pub-id"),
						Guid.NewGuid().ToString()
						),
					new XElement(dcNamespace + "title",title),
					new XElement(dcNamespace + "language",language),
					new XElement(opfNamespace + "meta",
						new XAttribute("property","dcterms:modified"),
						DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
						)
					);
			opf.Root.Add(metadata);

			//Adds the author, if exists
			if (author != null)
			{
                AddAuthor(author, "aut");
			}

			//Creates the manifest element and adds the nav file
			manifest = 
				new XElement(opfNamespace + "manifest",
					new XElement(opfNamespace + "item",
						new XAttribute("id","R1"),
						new XAttribute("href","nav.xhtml"),
						new XAttribute("media-type", "application/xhtml+xml"),
						new XAttribute("properties","nav")
					)
				);
			opf.Root.Add(manifest);

			//Creates the spine document
			spine = new XElement(opfNamespace + "spine");
			opf.Root.Add(spine);
		}

        #endregion //Constructors and initializers

        /// <summary>
		/// Create a new ID for the manifest
		/// </summary>
		/// <returns>The new generated id</returns>
		private string GetNewItemId()
        {
            //Gets all the existing items
            var items = opf
                .Root
                .Element(opfNamespace + "manifest")
                .Elements(opfNamespace + "item");
            //Get a new id
            int id = items.Count() > 0
                ? items.Select(x => int.Parse(((string)x.Attribute("id")).Substring(1))).Max() + 1
                : 1;

            //IDs can't have a number as it's first character, so we put "R" to begin
            return $"R{id}";
        }

        /// <summary>
        /// Sets if the navigation file must be present as a chapter.
        /// </summary>
        /// <param name="position"></param>
        public void SetNavigation(NavigationPosition position)
        {
            //Finds the navigation ID in the manifest
            string navId = (string)manifest
                .Elements(opfNamespace + "item")
                .FirstOrDefault(x =>x.HasAttributes && (string)x.Attribute("properties") == "nav")
                ?.Attribute("id");
            //The navigation must exists, otherwise throw exception
            if(navId == null)
            {
                throw new Exception("Could not find the navigation file in the manifest");
            }

            //Removes the navigation from the spine
            spine
                .Elements(opfNamespace + "itemref")
                .FirstOrDefault(x => x.HasAttributes && (string)x.Attribute("idref") == navId)
                ?.Remove();

            //If the navigation is set as FirstChapter, adds it to the spine
            if(position == NavigationPosition.FirstChapter)
            { 
                spine.AddFirst(
                    new XElement(opfNamespace + "itemref",
                        new XAttribute("idref",navId)
                    )
                );
            }
        }

        /// <summary>
        /// Add a element to the Spine and to the navigation, given it's id and navigation name
        /// </summary>
        /// <param name="id">Element ID</param>
        public void AddSpine(string id, string navigationName)
        {
            //Adds the element to the spine
            spine.Add(
                new XElement(opfNamespace + "itemref",
                    new XAttribute("idref", id)
                    )
                );

            //Adds the element to the navigation
            string navPath = (string)manifest
                .Elements(opfNamespace + "item")
                .First(x => (string)x.Attribute("id") == id).Attribute("href");
            nav.AddItem(navPath, navigationName);
        }

        /// <summary>
        /// Add a element to the Spine and to the navigation, using the file name as the navigation name
        /// </summary>
        /// <param name="id">Element ID</param>
        public void AddSpine(string id)
        {
            //Adds the element to the spine
            spine.Add(
                new XElement(opfNamespace + "itemref",
                    new XAttribute("idref", id)
                    )
                );

            //Adds the element to the navigation
            string navPath = (string)manifest
                .Elements(opfNamespace + "item")
                .First(x => (string)x.Attribute("id") == id).Attribute("href");
            nav.AddItem(navPath, Path.GetFileNameWithoutExtension(navPath));
        }
        
        /// <summary>
        /// Adds an author to the book with the specified role
        /// </summary>
        /// <param name="authorName">Author's name</param>
        /// <param name="role">Role</param>
        public void AddAuthor(string authorName, string role)
        {
            string creatorId = AddAuthor(authorName);
            metadata.Add(
                new XElement(opfNamespace + "meta",
                    new XAttribute("refines", $"#{creatorId}"),
                    new XAttribute("property", "role"),
                    new XAttribute("scheme", "marc:relators"),
                    new XAttribute("id", "role"),
                    role
                    )
                );
        }

        /// <summary>
        /// Adds an author to the book
        /// </summary>
        /// <param name="authorName">Author's name</param>
        public string AddAuthor(string authorName)
        {
            int i = 2;
            string creatorId = "creator";
            while(metadata
                .Elements(dcNamespace + "creator")
                .FirstOrDefault(x=> x.HasAttributes && (string)x.Attribute("id") == creatorId) != null)
            {
                creatorId = $"creator{i++}";
            }
            metadata.Add(
                    new XElement(dcNamespace + "creator",
                        new XAttribute("id", creatorId),
                        authorName
                        )
                    );
            return creatorId;
        }

        #region Add files

        /// <summary>
        /// Adds a xhtml, css or javascript file to the book. Returns the correponding file id.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="content"></param>
        /// <param name="mediaType"></param>
        /// <returns></returns>
        public string AddTextFile(string fileName, string content, TextMediaType mediaType)
        {
            //Gets the media-type
            string mType = "";
            switch (mediaType)
            {
                case TextMediaType.Css:
                    mType = "text/css";
                    break;
                case TextMediaType.Javascript:
                    mType = "application/javascript";
                    break;
                case TextMediaType.Xhtml:
                    mType = "application/xhtml+xml";
                    break;
            }
            //Adds the file to the zip package
            AddFile(Combine(ContentFolder, fileName), content);

            //Gets a new ID for the file
            string id = GetNewItemId();

            //Asdds the file to the manifest
            opf.Root.Element(opfNamespace + "manifest").Add(
                new XElement(opfNamespace + "item",
                    new XAttribute("id", id),
                    new XAttribute("href", fileName),
                    new XAttribute("media-type", mType)
                    )
                );
            return id;
        }
        
        /// <summary>
        /// Adds a image to the document. Returns the manifest file id.
        /// </summary>
        /// <param name="fileName">File name of the image</param>
        /// <param name="content">Image as a array of bytes</param>
        /// <param name="cover">True if the image is the epub cover. Default is false</param>
        /// <returns>Returns the ID of the added file</returns>
        public string AddImage(string fileName, byte[] content, bool cover = false)
        {
            //Adds the image to the zip package
            AddFile(Combine(ContentFolder, fileName), content);

            //Gets an ID for the image element
            string id = GetNewItemId();

            //Get the media-type
            string tp = Path.GetExtension(fileName).ToLower().Substring(1);
            if (tp == "jpg")
            {
                tp = "jpeg";
            }
            else if (tp == "svg")
            {
                tp = "svg+xml";
            }
            if (tp != "jpeg" && tp != "svg+xml" && tp != "png" && tp != "gif")
            {
                throw new ArgumentException("Epub supported types are jpeg, png, gif and svg", "fileName");
            }

            //Adds the item to the manifest
            XElement item =
                new XElement(opfNamespace + "item",
                    new XAttribute("id", id),
                    new XAttribute("href", fileName),
                    new XAttribute("media-type", $"image/{tp}")
                    );

            opf.Root.Element(opfNamespace + "manifest").Add(item);

            //Mark the image as the cover
            if (cover)
            {
                item.Add(new XAttribute("properties", "cover-image"));
            }

            //Returns the image ID
            return id;
        }

        /// <summary>
        /// Adds a file to the zip package
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="content">Content of the file</param>
        /// <param name="cl">Compression Level</param>
        private void AddFile(string fileName, string content, CompressionLevel cl)
		{
			var file = zip.CreateEntry(fileName, cl);
			using (StreamWriter sw = new StreamWriter(file.Open()))
			{
				sw.Write(content);
			}
		}

        /// <summary>
        /// Adds a file to the zip package
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="content">Content of the file</param>
        private void AddFile(string fileName, string content)
		{
			var file = zip.CreateEntry(fileName);
			using (StreamWriter sw = new StreamWriter(file.Open()))
			{
				sw.Write(content);
			}
		}

		/// <summary>
		/// Adds a file to the zip package
		/// </summary>
		/// <param name="fileName">File name</param>
		/// <param name="content">Content of the file</param>
		private void AddFile(string fileName, byte[] content)
		{
			var file = zip.CreateEntry(fileName);
			using (var os = file.Open())
			{
				os.Write(content, 0, content.Length);
			}
		}

        #endregion //Add Files

        /// <summary>
        /// Saves the epub
        /// </summary>
        public void Save(string fileName)
		{
            //Saves the navigation file
			AddFile(Combine(ContentFolder, "nav.xhtml"), nav.ToString());
			
			//Creates the container.xml file
			XDocument container = new XDocument(new XDeclaration("1.0", "utf-8", null));
			XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";
			container.Add(
				new XElement(ns + "container",
					new XAttribute("version", "1.0"),
					new XElement(ns + "rootfiles",
						new XElement(ns + "rootfile",
							new XAttribute("full-path", Combine(ContentFolder, "package.opf")),
							new XAttribute("media-type", "application/oebps-package+xml")
						)
					)
				)
			);
			AddFile(Combine(MetaFolder, "container.xml"), container.ToString());
			
            //Saves the package
			AddFile(Combine(ContentFolder, "package.opf"), opf.ToString());

			//Write the file from the memory stream to disk
			zip.Dispose();
			using(FileStream fs = new FileStream(fileName, FileMode.Create))
			{
				zipStream.WriteTo(fs);
			}
		}

		/// <summary>
		/// Disposes the resources
		/// </summary>
		public void Dispose()
		{
			zip.Dispose();
			zipStream.Dispose();
		}

        #region Auxiliar methods

        /// <summary>
        /// Creates a relative path, given a base path
        /// </summary>
        /// <param name="fullPath">Path to be made relative</param>
        /// <param name="root">Base path</param>
        /// <returns></returns>
        public string MakeRelative(string fullPath, string root)
		{
			if (!fullPath.StartsWith(root))
			{
				throw new ArgumentException("The path isn't child of the root", "fullPath");
			}
			string relativePath="";
			if (root.EndsWith("\\") || root.EndsWith("/"))
			{
				relativePath = fullPath.Substring(root.Length, fullPath.Length - root.Length);
			}
			else
			{
				relativePath = fullPath.Substring(root.Length+1, fullPath.Length - (root.Length+1));
			}
			return relativePath.Replace("\\","/");
		}

		/// <summary>
		///Combines paths with forward slashes
		/// </summary>
		/// <param name="path1">First Path</param>
		/// <param name="path2">Second Path</param>
		/// <returns>Returns a combined path</returns>
		public static string Combine(string path1, string path2)
		{
			string combinedPath = "";

            //Checks if the first path already ends with slashes to sse if it must be added
			if (path1.EndsWith("/") || path1.EndsWith("\\"))
			{
				combinedPath = path1 + path2;
			}
			else
			{
				combinedPath = path1 + "/" + path2;
			}

			return combinedPath.Replace("\\", "/");
		}

        #endregion //Auxiliar methods
    }
}
