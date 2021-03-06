﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using umbraco.cms.businesslogic.member;
using umbraco.cms.businesslogic;
using umbraco.BusinessLogic;
using System.Xml;

namespace uWiki.Businesslogic
{
    public class WikiFile
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string FileType { get; set; }
        public Guid NodeVersion { get; set; }
        public int CreatedBy { get; set; }
        public int RemovedBy { get; set; }
        public int NodeId { get; set; }

        public DateTime CreateDate { get; set; }
        public bool Current { get; set; }
        public int Downloads { get; set; }
        public bool Archived { get; set; }

        public bool Verified { get; set; }

        public List<UmbracoVersion> Versions { get; set; }
        public UmbracoVersion Version { get; set; }


        public void Delete()
        {
            if (File.Exists(HttpContext.Current.Server.MapPath(Path)))
                File.Delete(HttpContext.Current.Server.MapPath(Path));

            Data.SqlHelper.ExecuteNonQuery("DELETE FROM wikiFiles where ID = @id", Data.SqlHelper.CreateParameter("@id", Id));
        }


        public static List<WikiFile> CurrentFiles(int nodeId)
        {
            var wikiFiles = new List<WikiFile>();

            var reader = Data.SqlHelper.ExecuteReader("SELECT id FROM wikiFiles WHERE nodeId = @nodeid", Data.SqlHelper.CreateParameter("@nodeId", nodeId));

            while (reader.Read())
                wikiFiles.Add(new WikiFile(reader.GetInt("id")));

            return wikiFiles;
        }

        private readonly Events _events = new Events();

        private WikiFile() { }
        public static WikiFile Create(string name, Guid node, Guid memberGuid, HttpPostedFile file, string filetype, List<UmbracoVersion> versions)
        {
            try
            {
                var filename = file.FileName;
                var extension = filename.Substring(filename.LastIndexOf('.') + 1);

                if (ExtensionNotAllowed(extension))
                    return null;
                
                var content = Content.GetContentFromVersion(node);

                var member = new Member(memberGuid);

                if (content != null)
                {
                    var wikiFile = new WikiFile
                                   {
                                       Name = name,
                                       NodeId = content.Id,
                                       NodeVersion = content.Version,
                                       FileType = filetype,
                                       CreatedBy = member.Id,
                                       Downloads = 0,
                                       Archived = false,
                                       Versions = versions,
                                       Version = versions[0],
                                       Verified = false
                                   };

                    var path = string.Format("/media/wiki/{0}", content.Id);

                    if (Directory.Exists(HttpContext.Current.Server.MapPath(path)) == false)
                        Directory.CreateDirectory(HttpContext.Current.Server.MapPath(path));


                    file.SaveAs(HttpContext.Current.Server.MapPath(path));

                    wikiFile.Path = path;

                    wikiFile.Save();

                    return wikiFile;
                }

            }
            catch (Exception ex)
            {
                Log.Add(LogTypes.Debug, -1, ex.ToString());
            }

            return null;
        }

        private static bool ExtensionNotAllowed(string extension)
        {
            return extension == "html" || extension == "htm" || extension == "asp" || extension == "aspx";
        }

        public static WikiFile Create(string fileName, string extension, Guid node, Guid memberGuid, byte[] file, string filetype, List<UmbracoVersion> versions)
        {
            try
            {
                if (ExtensionNotAllowed(extension))
                    return null;

                var content = Content.GetContentFromVersion(node);
                var member = new Member(memberGuid);

                if (content != null)
                {
                    var wikiFile = new WikiFile
                                   {
                                       Name = fileName,
                                       NodeId = content.Id,
                                       NodeVersion = content.Version,
                                       FileType = filetype,
                                       CreatedBy = member.Id,
                                       Downloads = 0,
                                       Archived = false,
                                       Versions = versions,
                                       Version = versions[0],
                                       Verified = false
                                   };

                    var path = string.Format("/media/wiki/{0}", content.Id);

                    if (Directory.Exists(HttpContext.Current.Server.MapPath(path)) == false)
                        Directory.CreateDirectory(HttpContext.Current.Server.MapPath(path));

                    fileName = fileName.Substring(0, fileName.LastIndexOf('.') + 1);
                    path = string.Format("{0}/{1}_{2}.{3}", path, DateTime.Now.Ticks, umbraco.cms.helpers.url.FormatUrl(fileName), extension);

                    using (var fileStream = new FileStream(HttpContext.Current.Server.MapPath(path), FileMode.Create))
                        fileStream.Write(file, 0, file.Length);

                    wikiFile.Path = path;
                    wikiFile.Save();

                    return wikiFile;
                }
            }
            catch (Exception ex)
            {
                Log.Add(LogTypes.Debug, -1, ex.ToString());
            }

            return null;
        }

        public void Save()
        {
            if (Id == 0)
            {
                var e = new FileCreateEventArgs();
                FireBeforeCreate(e);
                if (e.Cancel)
                    return;

                Data.SqlHelper.ExecuteNonQuery(
                    "INSERT INTO wikiFiles (path, name, createdBy, nodeId, version, type, downloads, archived, umbracoVersion, verified) VALUES(@path, @name, @createdBy, @nodeId, @nodeVersion, @type, @downloads, @archived, @umbracoVersion, @verified)",
                    Data.SqlHelper.CreateParameter("@path", Path),
                    Data.SqlHelper.CreateParameter("@name", Name),
                    Data.SqlHelper.CreateParameter("@createdBy", CreatedBy),
                    Data.SqlHelper.CreateParameter("@nodeId", NodeId),
                    Data.SqlHelper.CreateParameter("@type", FileType),
                    Data.SqlHelper.CreateParameter("@nodeVersion", NodeVersion),
                    Data.SqlHelper.CreateParameter("@downloads", Downloads),
                    Data.SqlHelper.CreateParameter("@archived", Archived),
                    Data.SqlHelper.CreateParameter("@umbracoVersion", ToVersionString(Versions)),
                    Data.SqlHelper.CreateParameter("@verified", Verified)
                    );

                CreateDate = DateTime.Now;

                Id = Data.SqlHelper.ExecuteScalar<int>("SELECT MAX(id) FROM wikiFiles WHERE createdBy = @createdBy", Data.SqlHelper.CreateParameter("@createdBy", CreatedBy));

                FireAfterCreate(e);
            }
            else
            {
                var e = new FileUpdateEventArgs();
                FireBeforeUpdate(e);

                if (e.Cancel)
                    return;

                Data.SqlHelper.ExecuteNonQuery(
                    "UPDATE wikiFiles SET path = @path, name = @name, type = @type, [current] = @current, removedBy = @removedBy, version = @version, downloads = @downloads, archived = @archived, umbracoVersion = @umbracoVersion, verified = @verified WHERE id = @id",
                    Data.SqlHelper.CreateParameter("@path", Path),
                    Data.SqlHelper.CreateParameter("@name", Name),
                    Data.SqlHelper.CreateParameter("@type", FileType),
                    Data.SqlHelper.CreateParameter("@current", Current),
                    Data.SqlHelper.CreateParameter("@removedBy", RemovedBy),
                    Data.SqlHelper.CreateParameter("@version", NodeVersion),
                    Data.SqlHelper.CreateParameter("@id", Id),
                    Data.SqlHelper.CreateParameter("@downloads", Downloads),
                    Data.SqlHelper.CreateParameter("@archived", Archived),
                    Data.SqlHelper.CreateParameter("@umbracoVersion", ToVersionString(Versions)),
                    Data.SqlHelper.CreateParameter("@verified", Verified)
                    );

                FireAfterUpdate(e);
            }
        }



        //available umbraco version strings: v4, v45, v3
        public static WikiFile FindPackageForUmbracoVersion(int nodeid, string umbracoVersion)
        {
            return FindRelatedFileForUmbracoVersion(nodeid, umbracoVersion, "package");
        }

        public static WikiFile FindPackageDocumentationForUmbracoVersion(int nodeid, string umbracoVersion)
        {
            return FindRelatedFileForUmbracoVersion(nodeid, umbracoVersion, "docs");
        }

        private static WikiFile FindRelatedFileForUmbracoVersion(int nodeid, string umbracoVersion, string fileType)
        {
            try
            {
                //try find one based on the specific version first
                var id = Application.SqlHelper.ExecuteScalar<int>(
                    "Select TOP 1 id from wikiFiles where nodeId = @nodeid and type = @packageType and (umbracoVersion like @umbracoVersion) ORDER BY createDate DESC",
                    Application.SqlHelper.CreateParameter("@nodeid", nodeid),
                    Application.SqlHelper.CreateParameter("@packageType", fileType),
                    Application.SqlHelper.CreateParameter("@umbracoVersion", "%" + umbracoVersion + "%")
                    );

                if (id > 0)
                    return new WikiFile(id);

                //if a version specific file wasnt found try and find one based on nan
                id = Application.SqlHelper.ExecuteScalar<int>(
                    "Select TOP 1 id from wikiFiles where nodeId = @nodeid and type = @packageType and (umbracoVersion like '%nan%') ORDER BY createDate DESC",
                    Application.SqlHelper.CreateParameter("@nodeid", nodeid),
                    Application.SqlHelper.CreateParameter("@packageType", fileType)
                    );

                if (id > 0)
                    return new WikiFile(id);
            }
            catch
            {
                return null;
            }

            return null;
        }

        public WikiFile(int id)
        {
            using (var reader = Data.SqlHelper.ExecuteReader("SELECT * FROM wikiFiles WHERE id = @fileId",
                    Application.SqlHelper.CreateParameter("@fileId", id)))
            {
                if (reader.Read())
                {
                    Id = reader.GetInt("id");
                    Path = reader.GetString("path");
                    Name = reader.GetString("name");
                    FileType = reader.GetString("type");
                    RemovedBy = reader.GetInt("removedBy");
                    CreatedBy = reader.GetInt("createdBy");
                    NodeVersion = reader.GetGuid("version");
                    NodeId = reader.GetInt("nodeId");
                    CreateDate = reader.GetDateTime("createDate");
                    Current = reader.GetBoolean("current");
                    Downloads = reader.GetInt("downloads");
                    Archived = reader.GetBoolean("archived");
                    Verified = reader.GetBoolean("verified");
                    Versions = GetVersionsFromString(reader.GetString("umbracoVersion"));
                    Version = Versions.Any()
                        ? GetVersionsFromString(reader.GetString("umbracoVersion"))[0]
                        : UmbracoVersion.DefaultVersion();
                }
                else
                {
                    throw new ArgumentException(string.Format("No node exists with id '{0}'", Id));
                }
            }
        }

        public void UpdateDownloadCounter()
        {
            UpdateDownloadCount(this.Id, false, true);
        }

        public void UpdateDownloadCounter(bool ignoreCookies, bool isPackage)
        {
            UpdateDownloadCount(this.Id, ignoreCookies, isPackage);
        }

        public static List<UmbracoVersion> GetVersionsFromString(string p)
        {
            var umbracoVersions = new List<UmbracoVersion>();

            foreach (var ver in p.Split(','))
            {
                if (UmbracoVersion.AvailableVersions().ContainsKey(ver))
                    umbracoVersions.Add(UmbracoVersion.AvailableVersions()[ver]);
            }

            return umbracoVersions;
        }

        public static string ToVersionString(List<UmbracoVersion> versions)
        {
            var umbracoVersions = string.Empty;

            foreach (var ver in versions)
                umbracoVersions += string.Format("{0},", ver.Version);

            return umbracoVersions.TrimEnd(',');

        }

        public static void UpdateDownloadCount(int fileId, bool ignoreCookies, bool isPackage)
        {
            var cookie = HttpContext.Current.Request.Cookies["ProjectFileDownload" + fileId];

            if (cookie != null && ignoreCookies == false)
                return;

            var downloads = Application.SqlHelper.ExecuteScalar<int>("Select downloads from wikiFiles where id = @id;", Application.SqlHelper.CreateParameter("@id", fileId));
            downloads = downloads + 1;

            Application.SqlHelper.ExecuteNonQuery(
                "update wikiFiles set downloads = @downloads where id = @id;",
                Application.SqlHelper.CreateParameter("@id", fileId),
                Application.SqlHelper.CreateParameter("@downloads", downloads));

            if (isPackage)
            {
                var currentMember = 0;
                var member = Member.GetCurrentMember();
                if (member != null)
                    currentMember = member.Id;

                //update download count update
                Application.SqlHelper.ExecuteNonQuery(
                    @"insert into projectDownload(projectId,memberId,timestamp) 
                        values((select nodeId from wikiFiles where id = @id) ,@memberId, getdate())",
                    Application.SqlHelper.CreateParameter("@id", fileId),
                    Application.SqlHelper.CreateParameter("@memberId", currentMember));
            }

            cookie = new HttpCookie("ProjectFileDownload" + fileId) { Expires = DateTime.Now.AddHours(1) };
            HttpContext.Current.Response.Cookies.Add(cookie);
        }

        public byte[] ToByteArray()
        {
            var path = HttpContext.Current.Server.MapPath(this.Path);

            byte[] packageByteArray;

            using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read))
            {
                packageByteArray = new byte[fileStream.Length];

                fileStream.Read(packageByteArray, 0, (int) fileStream.Length);
            }

            return packageByteArray;
        }
        
        public XmlNode ToXml(XmlDocument d)
        {
            XmlNode toXml = d.CreateElement("wikiFile");
            
            toXml.Attributes.Append(umbraco.xmlHelper.addAttribute(d, "id", Id.ToString()));
            toXml.Attributes.Append(umbraco.xmlHelper.addAttribute(d, "name", Name));
            toXml.Attributes.Append(umbraco.xmlHelper.addAttribute(d, "created", CreateDate.ToString()));

            toXml.AppendChild(umbraco.xmlHelper.addCDataNode(d, "path", Path));
            toXml.AppendChild(umbraco.xmlHelper.addCDataNode(d, "verified", Verified.ToString()));

            return toXml;
        }

        /* EVENTS */
        public static event EventHandler<FileCreateEventArgs> BeforeCreate;
        protected virtual void FireBeforeCreate(FileCreateEventArgs e)
        {
            _events.FireCancelableEvent(BeforeCreate, this, e);
        }
        public static event EventHandler<FileCreateEventArgs> AfterCreate;
        protected virtual void FireAfterCreate(FileCreateEventArgs e)
        {
            if (AfterCreate != null)
                AfterCreate(this, e);
        }

        public static event EventHandler<FileRemoveEventArgs> BeforeRemove;
        protected virtual void FireBeforeDelete(FileRemoveEventArgs e)
        {
            _events.FireCancelableEvent(BeforeRemove, this, e);
        }
        public static event EventHandler<FileRemoveEventArgs> AfterRemove;
        protected virtual void FireAfterDelete(FileRemoveEventArgs e)
        {
            if (AfterRemove != null)
                AfterRemove(this, e);
        }

        public static event EventHandler<FileUpdateEventArgs> BeforeUpdate;
        protected virtual void FireBeforeUpdate(FileUpdateEventArgs e)
        {
            _events.FireCancelableEvent(BeforeUpdate, this, e);
        }
        public static event EventHandler<FileUpdateEventArgs> AfterUpdate;
        protected virtual void FireAfterUpdate(FileUpdateEventArgs e)
        {
            if (AfterUpdate != null)
                AfterUpdate(this, e);
        }

    }
}
