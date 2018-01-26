using System.Xml.Linq;

namespace EpubWriter
{
    public class Navigation
	{
        #region Private variables
        
        //Namespaces
        private readonly XNamespace ns = @"http://www.w3.org/1999/xhtml";
        private readonly XNamespace nsEpub = @"http://www.idpf.org/2007/ops";
        
        private XDocument doc;
        private XElement ol;

        #endregion

        /// <summary>
        /// Creates a new Navigation instance
        /// </summary>
        public Navigation()
		{
            doc = new XDocument();
            
            //Creates and add the XHTML5 document declaration
            XDocumentType documentType = new XDocumentType("html", null, null, null);
            doc.Add(documentType);

            //Adds the root element
            XElement root = new XElement(ns + "html");
            doc.Add(root);

            //Adds the head element with the title "Navigation"
            XElement head = new XElement(ns + "head",
                new XElement(ns + "title", "Navigation"),
                new XElement(ns + "meta",
                    new XAttribute("http-equiv", "content-type"),
                    new XAttribute("content", "text/html; charset=UTF-8")
                    )
                );
            root.Add(head);

            //Adds the body element
            XElement body = new XElement(ns + "body");
            root.Add(body);

            //Adds the epub namespace to the root element
            doc.Root.Add(new XAttribute(XNamespace.Xmlns + "epub", nsEpub));
            
            //Creates the list element
			ol = new XElement(ns + "ol");

            //Adds the nav element and add the list to it
			doc.Root.Element(ns + "body").Add(
				new XElement(ns + "nav",
					new XAttribute(nsEpub + "type", "toc"),
					new XAttribute("id", "toc"),
					ol
				)
			);
            
		}

        /// <summary>
        /// Adds a new item to the root navigation list
        /// </summary>
        /// <param name="path">Path to the item relative to the Navigation file path</param>
        /// <param name="text">Section text to display</param>
        public void AddItem(string path, string text)
		{
			ol.Add(
				new XElement(ns + "li",
					new XElement(ns + "a",
						new XAttribute("href", path),
						text
					)
				)
			);
		}

        /// <summary>
        /// Adds a new item to the navigation
        /// </summary>
        /// <param name="path">Path to the item relative to the Navigation file path</param>
        /// <param name="text">Section text to display</param>
        /// <param name="parent">The parent section to add the item to</param>
        public void AddItem(string path, string text, XElement parent)
        {
            parent.Add(
                new XElement(ns + "li",
                    new XElement(ns + "a",
                        new XAttribute("href", path),
                        text
                    )
                )
            );
        }

        /// <summary>
        /// Adds a new item to the navigation
        /// </summary>
        /// <param name="path">Path to the item relative to the Navigation file path</param>
        /// <param name="text">Section text to display</param>
        /// <param name="parent">The parent section to add the item to</param>
        /// <returns>Returns the new parent item</returns>
        public XElement AddParentItem(string path, string text, XElement parent)
        {
            XElement newParent = new XElement(ns + "ol");
            
            parent.Add(
                new XElement(ns + "li",
                    new XElement(ns + "span",
                        new XAttribute("href", path),
                        text,
                        newParent
                    )
                )
            );
            return newParent;
        }

        /// <summary>
        /// Adds a new item to the navigation
        /// </summary>
        /// <param name="path">Path to the item relative to the Navigation file path</param>
        /// <param name="text">Section text to display</param>
        /// <param name="parent">The parent section to add the item to</param>
        /// <returns>Returns the new parent item</returns>
        public XElement AddParentItem(string path, string text)
        {
            XElement newParent = new XElement(ns + "ol");
            
            ol.Add(
                new XElement(ns + "li",
                    new XElement(ns + "span",
                        new XAttribute("href", path),
                        text,
                        newParent
                    )
                )
            );
            return newParent;
        }

        /// <summary>
        /// Returns the file content as a string
        /// </summary>
        /// <returns>Returns the file content as a string</returns>
        public override string ToString()
        {
            return doc.ToString();
        }
    }
}
