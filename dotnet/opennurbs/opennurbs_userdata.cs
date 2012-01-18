using System;

namespace Rhino.DocObjects.Custom
{
  /// <summary>
  /// Provides a base class for custom classes of information which may be attached to
  /// geometry or attribute classes.
  /// </summary>
  public abstract class UserData : IDisposable
  {
    static int m_next_serial_number = 1;
    int m_serial_number=-1;
    IntPtr m_pNativePointer = IntPtr.Zero;

    /// <summary>
    /// Protected constructor for inheriting classes.
    /// </summary>
    protected UserData() { }

    #region IDisposable implementation
    /// <summary>
    /// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
    /// </summary>
    ~UserData()
    {
      Dispose(false);
    }

    /// <summary>
    /// Actively reclaims unmanaged resources that this instance uses.
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// For derived class implementers.
    /// <para>This method is called with argument true when class user calls Dispose(), while with argument false when
    /// the Garbage Collector invokes the finalizer, or Finalize() method.</para>
    /// <para>You must reclaim all used unmanaged resources in both cases, and can use this chance to call Dispose on disposable fields if the argument is true.</para>
    /// <para>Also, you must call the base virtual method within your overriding method.</para>
    /// </summary>
    /// <param name="disposing">true if the call comes from the Dispose() method; false if it comes from the Garbage Collector finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
      if (IntPtr.Zero == m_pNativePointer)
        return;

      // Leak for now. We want to make sure that the class is not
      // attached to an object before deleting.
      // UnsafeNativeMethods.CRhCmnUserData_Delete(m_pNativePointer, m_serial_number);
      m_pNativePointer = IntPtr.Zero;
    }
    #endregion


    internal virtual IntPtr NonConstPointer(bool createIfMissing)
    {
#if RHINO_SDK
      if (createIfMissing && IntPtr.Zero == m_pNativePointer)
      {
        m_serial_number = m_next_serial_number++;
        Type t = this.GetType();
        Guid managed_type_id = t.GUID;
        string description = Description;

        if (this is UserDictionary)
        {
          Guid id = Rhino.RhinoApp.Rhino5Id;
          m_pNativePointer = UnsafeNativeMethods.CRhCmnUserData_New(m_serial_number, managed_type_id, id, description);
        }
        else
        {
          Rhino.PlugIns.PlugIn plugin = PlugIns.PlugIn.Find(t.Assembly);
          Guid plugin_id = plugin.Id;
          m_pNativePointer = UnsafeNativeMethods.CRhCmnUserData_New(m_serial_number, managed_type_id, plugin_id, description);
        }
      }
#endif
      return m_pNativePointer;
    }

    /// <summary>Descriptive name of the user data.</summary>
    public virtual string Description { get { return "RhinoCommon UserData"; } }

    /// <summary>
    /// If you want to save this user data in a 3dm file, override
    /// ShouldWrite and return true.  If you do support serialization,
    /// you must also override the Read and Write functions.
    /// </summary>
    public virtual bool ShouldWrite { get { return false; } }

    /// <summary>Writes the content of this data to a stream archive.</summary>
    /// <param name="archive">An archive.</param>
    /// <returns>true if the data was successfully written. The default implementation always returns false.</returns>
    protected virtual bool Write(Rhino.FileIO.BinaryArchiveWriter archive) { return false; }

    /// <summary>Reads the content of this data from a stream archive.</summary>
    /// <param name="archive">An archive.</param>
    /// <returns>true if the data was successfully written. The default implementation always returns false.</returns>
    protected virtual bool Read(Rhino.FileIO.BinaryArchiveReader archive) { return false; }

    /// <summary>
    /// Is called when the object associated with this data is transformed.
    /// </summary>
    /// <param name="transform">The transform being applied.</param>
    protected virtual void OnTransform(Rhino.Geometry.Transform transform) { }

    /// <summary>
    /// Is called when the object is being duplicated.
    /// </summary>
    /// <param name="source">The source data.</param>
    protected virtual void OnDuplicate(UserData source) { }

    internal delegate void TransformUserDataCallback(int serial_number, ref Rhino.Geometry.Transform xform);
    internal delegate int ArchiveUserDataCallback(int serial_number);
    internal delegate int ReadWriteUserDataCallback(int serial_number, int writing, IntPtr pBinaryArchive);
    internal delegate int DuplicateUserDataCallback(int serial_number, IntPtr pNativeUserData);
    internal delegate IntPtr CreateUserDataCallback(Guid managed_type_id);
    internal delegate void DeleteUserDataCallback(int serial_number);

    private static TransformUserDataCallback m_OnTransformUserData;
    private static ArchiveUserDataCallback m_OnArchive;
    private static ReadWriteUserDataCallback m_OnReadWrite;
    private static DuplicateUserDataCallback m_OnDuplicate;
    private static CreateUserDataCallback m_OnCreate;
    private static DeleteUserDataCallback m_OnDelete;

    private static void OnTransformUserData(int serial_number, ref Rhino.Geometry.Transform xform)
    {
      UserData ud = FromSerialNumber(serial_number);
      if (ud!=null)
      {
        try
        {
          ud.OnTransform(xform);
        }
        catch (Exception ex)
        {
          Runtime.HostUtils.ExceptionReport(ex);
        }
      }
    }
    private static int OnArchiveUserData(int serial_number)
    {
      int rc = 0; //FALSE
      UserData ud = FromSerialNumber(serial_number);
      if (ud != null)
      {
        try
        {
          // the user data class MUST have a GuidAttribute in order to write
          object[] attr = ud.GetType().GetCustomAttributes(typeof(System.Runtime.InteropServices.GuidAttribute), false);
          if( attr.Length==1 )
            rc = ud.ShouldWrite ? 1 : 0;
        }
        catch (Exception ex)
        {
          Runtime.HostUtils.ExceptionReport(ex);
        }
      }
      return rc;
    }
    private static int OnReadWriteUserData(int serial_number, int writing, IntPtr pBinaryArchive)
    {
      int rc = 0; //FALSE
      UserData ud = FromSerialNumber(serial_number);
      if (ud != null)
      {
        try
        {
          if (0 == writing)
          {
            Rhino.FileIO.BinaryArchiveReader reader = new FileIO.BinaryArchiveReader(pBinaryArchive);
            rc = ud.Read(reader) ? 1 : 0;
          }
          else
          {
            Rhino.FileIO.BinaryArchiveWriter writer = new FileIO.BinaryArchiveWriter(pBinaryArchive);
            rc = ud.Write(writer) ? 1 : 0;
          }
        }
        catch (Exception ex)
        {
          Runtime.HostUtils.ExceptionReport(ex);
        }
      }
      return rc;
    }
    private static int OnDuplcateUserData(int serial_number, IntPtr pNativeUserData)
    {
      int rc = 0;
      UserData ud = FromSerialNumber(serial_number);
      if (ud != null)
      {
        try
        {
          Type t = ud.GetType();
          UserData new_ud = System.Activator.CreateInstance(t) as UserData;
          new_ud.m_serial_number = UserData.m_next_serial_number++;
          new_ud.m_pNativePointer = pNativeUserData;
          UserData.StoreInRuntimeList(new_ud);
          new_ud.OnDuplicate(ud);
          rc = new_ud.m_serial_number;
        }
        catch (Exception ex)
        {
          Runtime.HostUtils.ExceptionReport(ex);
        }
      }
      return rc;
    }
    private static IntPtr OnCreateInstance(Guid managedTypeId)
    {
      IntPtr rc = IntPtr.Zero;
      Type t = null;
      for (int i = 0; i < m_types.Count; i++)
      {
        if (m_types[i].GUID == managedTypeId)
        {
          t = m_types[i];
          break;
        }
      }
      if (t != null)
      {
        try
        {
          UserData ud = System.Activator.CreateInstance(t) as UserData;
          if (ud != null)
          {
            rc = ud.NonConstPointer(true);
            if (ud.m_serial_number > 0)
              m_attached_custom_user_datas.Add(ud.m_serial_number, ud);
          }
        }
        catch (Exception ex)
        {
          Runtime.HostUtils.ExceptionReport(ex);
        }
      }
      return rc;
    }
    private static void OnDelete(int serial_number)
    {
      UserData ud = UserData.FromSerialNumber(serial_number);
      if( ud!=null )
      {
        ud.m_pNativePointer = IntPtr.Zero;
        GC.SuppressFinalize(ud);
        UserData.m_attached_custom_user_datas.Remove(serial_number);
      }
    }

    static readonly System.Collections.Generic.List<Type> m_types = new System.Collections.Generic.List<Type>();
    internal static void RegisterType(Type t)
    {
      m_types.Add(t);

      m_OnTransformUserData = OnTransformUserData;
      m_OnArchive = OnArchiveUserData;
      m_OnReadWrite = OnReadWriteUserData;
      m_OnDuplicate = OnDuplcateUserData;
      m_OnCreate = OnCreateInstance;
      m_OnDelete = OnDelete;
      UnsafeNativeMethods.CRhCmnUserData_SetCallbacks(m_OnTransformUserData, m_OnArchive, m_OnReadWrite, m_OnDuplicate, m_OnCreate, m_OnDelete);
    }
    #region statics

    static readonly System.Collections.Generic.Dictionary<int, UserData> m_attached_custom_user_datas = new System.Collections.Generic.Dictionary<int, UserData>();
    internal static UserData FromSerialNumber(int serial_number)
    {
      if (serial_number < 1)
        return null;
      UserData rc;
      if (m_attached_custom_user_datas.TryGetValue(serial_number, out rc))
        return rc;

      return null;
    }
    internal static void StoreInRuntimeList(UserData ud)
    {
      m_attached_custom_user_datas[ud.m_serial_number] = ud;
    }


    /// <summary>
    /// Expert user tool that copies user data that has a positive 
    /// CopyCount from the source object to a destination object.
    /// Generally speaking you don't need to use Copy().
    /// Simply rely on things like the copy constructors to do the right thing.
    /// </summary>
    /// <param name="source">A source object for the data.</param>
    /// <param name="destination">A destination object for the data.</param>
    public static void Copy(Rhino.Runtime.CommonObject source, Rhino.Runtime.CommonObject destination)
    {
      IntPtr pConstSource = source.ConstPointer();
      IntPtr pDestination = destination.NonConstPointer();
      UnsafeNativeMethods.ON_Object_CopyUserData(pConstSource, pDestination);
    }

    /// <summary>
    /// Moves the user data from objectWithUserData to a temporary data storage
    /// identifierd by the return Guid.  When MoveUserDataFrom returns, the
    /// objectWithUserData will not have any user data.
    /// </summary>
    /// <param name="objectWithUserData">Object with user data attached.</param>
    /// <returns>
    /// Guid identifier for storage of UserData that is held in a temporary list
    /// by this class. This function should be used in conjunction with MoveUserDataTo
    /// to transfer the user data to a different object.
    /// Returns Guid.Empty if there was no user data to transfer.
    /// </returns>
    public static Guid MoveUserDataFrom(Rhino.Runtime.CommonObject objectWithUserData)
    {
      Guid id = Guid.NewGuid();
      IntPtr pConstObject = objectWithUserData.ConstPointer();
      if (UnsafeNativeMethods.ON_UserDataHolder_MoveUserDataFrom(id, pConstObject))
        return id;
      return Guid.Empty;
    }
    
    /// <summary>
    /// Moves the user data.
    /// <para>See <see cref="MoveUserDataFrom"/> for more information.</para>
    /// </summary>
    /// <param name="objectToGetUserData">Object data source.</param>
    /// <param name="id">Target.</param>
    /// <param name="append">If the data should be appended or replaced.</param>
    public static void MoveUserDataTo(Rhino.Runtime.CommonObject objectToGetUserData, Guid id, bool append)
    {
      if (id != Guid.Empty)
      {
        IntPtr pConstObject = objectToGetUserData.ConstPointer();
        UnsafeNativeMethods.ON_UserDataHolder_MoveUserDataTo(id, pConstObject, append);
      }
    }
    #endregion
  }

  /// <summary>
  /// Represents user data with unknown origin.
  /// </summary>
  public class UnknownUserData : UserData
  {
    /// <summary>
    /// Constructs a new unknown data entity.
    /// </summary>
    /// <param name="pNativeUserData">A pointer to the entity.</param>
    public UnknownUserData(IntPtr pNativeUserData)
    {
    }
  }

  /// <summary>
  /// Represets a regulated collection of user data.
  /// </summary>
  public class UserDataList
  {
    readonly Rhino.Runtime.CommonObject m_parent;
    internal UserDataList(Rhino.Runtime.CommonObject parent)
    {
      m_parent = parent;
    }

    /// <summary>
    /// Number of UserData objects in this list.
    /// </summary>
    public int Count
    {
      get
      {
        IntPtr pOnObject = m_parent.ConstPointer();
        return UnsafeNativeMethods.ON_Object_UserDataCount(pOnObject);
      }
    }

    /// <summary>
    /// If the userdata is already in a different UserDataList, it
    /// will be removed from that list and added to this list.
    /// </summary>
    /// <param name="userdata">Data element.</param>
    /// <returns>Whether this operation succeeded.</returns>
    public bool Add(UserData userdata)
    {
      if (!(userdata is SharedUserDictionary))
      {
        Type t = userdata.GetType();
        System.Reflection.ConstructorInfo constructor = t.GetConstructor(System.Type.EmptyTypes);
        if (!t.IsPublic || constructor == null)
          throw new ArgumentException("userdata must be a public class and have a parameterless constructor");
      }
      IntPtr pOnObject = m_parent.ConstPointer();
      IntPtr pUserData = userdata.NonConstPointer(true);
      const bool detachIfNeeded = true;
      bool rc = UnsafeNativeMethods.ON_Object_AttachUserData(pOnObject, pUserData, detachIfNeeded);
      if (rc)
        UserData.StoreInRuntimeList(userdata);
      return rc;
    }
/*
    public void Remove(UserData userdata)
    {
      IntPtr pOnObject = m_parent.ConstPointer();
      IntPtr pUserData = userdata.NonConstPointer(false);
      bool success = UnsafeNativeMethods.ON_Object_DetachUserData(pOnObject, pUserData);
      if (success)
        userdata.OnDetachFromList();
    }
*/

    /// <summary>
    /// Finds a specific data type in this regulated collection.
    /// </summary>
    /// <param name="userdataType">A data type.</param>
    /// <returns>The found data, or null of nothing was found.</returns>
    public UserData Find(Type userdataType)
    {
      if (!userdataType.IsSubclassOf(typeof(UserData)))
        return null;
      Guid id = userdataType.GUID;
      IntPtr pOnObject = m_parent.ConstPointer();
      int serial_number = UnsafeNativeMethods.CRhCmnUserData_Find(pOnObject, id);
      return UserData.FromSerialNumber(serial_number);
    }
  }

  /// <summary>
  /// Defines the storage data class for a <see cref="Rhino.Collections.ArchivableDictionary">user dictionary</see>.
  /// </summary>
  [System.Runtime.InteropServices.Guid("171E831F-7FEF-40E2-9857-E5CCD39446F0")]
  public class UserDictionary : UserData
  {
    Rhino.Collections.ArchivableDictionary m_dictionary;
    /// <summary>
    /// Gets the dictionary that is associated with this class.
    /// <para>This dictionary is unique.</para>
    /// </summary>
    public Rhino.Collections.ArchivableDictionary Dictionary
    {
      get { return m_dictionary??(m_dictionary=new Collections.ArchivableDictionary()); }
    }

    /// <summary>
    /// Gets the text "RhinoCommon UserDictionary".
    /// </summary>
    public override string Description
    {
      get
      {
        return "RhinoCommon UserDictionary";
      }
    }

    /// <summary>
    /// Clones the user data.
    /// </summary>
    /// <param name="source">The source data.</param>
    protected override void OnDuplicate(UserData source)
    {
      UserDictionary dict = source as UserDictionary;
      if (dict != null)
        m_dictionary = dict.m_dictionary.Clone();
    }

    /// <summary>
    /// Writes this entity if the count is larger than 0.
    /// </summary>
    public override bool ShouldWrite
    {
      get { return m_dictionary.Count > 0; }
    }

    /// <summary>
    /// Is called to read this entity.
    /// </summary>
    /// <param name="archive">An archive.</param>
    /// <returns>Always returns true.</returns>
    protected override bool Read(FileIO.BinaryArchiveReader archive)
    {
      m_dictionary = archive.ReadDictionary();
      return true;
    }

    /// <summary>
    /// Is called to write this entity.
    /// </summary>
    /// <param name="archive">An archive.</param>
    /// <returns>Always returns true.</returns>
    protected override bool Write(FileIO.BinaryArchiveWriter archive)
    {
      archive.WriteDictionary(Dictionary);
      return true;
    }
  }

  [System.Runtime.InteropServices.Guid("2544A64E-220D-4D65-B8D4-611BB57B46C7")]
  class SharedUserDictionary : UserDictionary
  {
  }
}