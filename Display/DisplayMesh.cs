//
// DisplayMesh.cs
// RhinoMobile.Display
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Display;

#region OpenTK-1.0 API diffs
// This region handles differences between OpenTK-1.0 on MonoDroid and MonoTouch.  
// MonoDroid is behind the times and has not yet caught up with MonoTouch
// on the OpenTK-1.0 front.  Once things stabilize, this can be removed. 
// See this thread for details:
// http://forums.xamarin.com/discussion/1939/renderbuffertarget-in-opentk
#if __ANDROID__
using RenderBufferTarget = OpenTK.Graphics.ES20.All;
using BufferTarget = OpenTK.Graphics.ES20.All;
using BufferUsage = OpenTK.Graphics.ES20.All;
using VertexAttribPointerType = OpenTK.Graphics.ES20.All;
using ShaderType = OpenTK.Graphics.ES20.All;
using EnableCap = OpenTK.Graphics.ES20.All;
using ProgramParameter = OpenTK.Graphics.ES20.All;
using ShaderParameter = OpenTK.Graphics.ES20.All;
using GetError = OpenTK.Graphics.ES20.All;
using Color4 = OpenTK.Graphics.ES20.All;
using DepthFunction = OpenTK.Graphics.ES20.All;
using BlendingFactorSrc = OpenTK.Graphics.ES20.All;
using BlendingFactorDest = OpenTK.Graphics.ES20.All;
#endif
#endregion

namespace RhinoMobile.Display
{
	/// <summary>
	/// For the storage of global variables from within the Display namespace.
	/// </summary>
	public static class Globals
	{
		// Analysis disable InconsistentNaming
		public const uint UNSET_HANDLE = 4294967295;
		public const double ON_ZERO_TOLERANCE = 2.3283064365386962890625e-10;
		// Analysis restore InconsistentNaming
	}

	[Serializable()]
	public struct VData {
		public Point3f Vertex;
	}

	[Serializable()]
	public struct VNData  {
		public Point3f Vertex;
		public Vector3f Normal;
	}
	
	[Serializable()]
	public struct VCData {
		public Point3f Vertex;
		public Color4f Color;
	}

	[Serializable()]
	public struct VNCData {
		public Point3f Vertex;
		public Vector3f Normal;
		public Color4f Color;
	}
	
	[Serializable()]
	public class DisplayMesh : DisplayObject, IDisposable
	{
		#region members
		readonly int m_partitionIndex;
		bool m_initializationFailed;

		// handles to the vbos
		uint m_vertex_buffer_handle = Globals.UNSET_HANDLE;
		uint m_index_buffer_handle = Globals.UNSET_HANDLE;

		// stubbed for serialization
		//byte[] m_vertexBufferData;
		//byte[] m_normalBufferData;
		//byte[] m_vertexAndNormalBufferData;
		//byte[] m_indexBufferData;
		#endregion

		#region properties
		/// <value> The underlying openNURBS mesh associated with this runtime DisplayMesh </value>
		public new Mesh Mesh { get; set; }

		/// <value> The object Id associated with the mesh in the openNURBS archive. </value>
		public Guid FileObjectId { get; set; }

		/// <value> OpenGL ES 2.0 vertex buffer handle </value>
		public uint VertexBufferHandle
		{
			get { return m_vertex_buffer_handle; }
			set
			{
				if (m_vertex_buffer_handle != Globals.UNSET_HANDLE && value != Globals.UNSET_HANDLE)
					throw new Exception ("Attempting to overwrite a handle");
				m_vertex_buffer_handle = value;
			}
		}

		/// <value> OpenGL ES 2.0 index buffer handle </value>
		public uint IndexBufferHandle
		{
			get { return m_index_buffer_handle; }
			set
			{
				if (m_index_buffer_handle != Globals.UNSET_HANDLE && value != Globals.UNSET_HANDLE)
					throw new Exception ("Attempting to overwrite a handle");
				m_index_buffer_handle = value;
			}
		}

		/// <value> The int length of the buffer for the vertex indices. </value>
		public int IndexBufferLength { get; set; }

		/// <value> The vertex values that make up the position data on this mesh. </value>
		public VData[] Vertices { get; private set; }

		/// <value> An array of interleaved vertices and normals. </value>
		public VNData[] VerticesNormals { get; private set; }

		/// <value> An array of interleaved vertices and colors. </value>
		public VCData[] VerticesColors { get; private set; }

		/// <value> An array of interleaved vertices, normals, and colors. </value>
		public VNCData[] VerticesNormalsColors { get; private set; }

		/// <value> The index values that make up the index data on this mesh. </value>
		public ushort[] Indices { get; private set; }

		/// <value> NOT YET IMPLEMENTED.  Set to true if you want VBO data dumped to a byte[] for serialization.</value>
		public bool CaptureVBOData { get; set; }

		/// <summary>
		/// <para>NOT YET IMPLEMENTED.</para>
		/// VertexBufferData for Serialization purposes.  Only set if CaptureVBOData is true.
		/// <para>Rhino.FileIO has methods for reading and writing ByteArrays</para>
		/// </summary>
		public byte[] VertexBufferData { get; private set; }

		/// <summary>
		/// NormalBufferData for Serialization purposes.  Only set if CaptureVBOData is true.
		/// <para>Rhino.FileIO has methods for reading and writing ByteArrays</para>
		/// </summary>
		public byte[] NormalBufferData { get; private set; }

		/// <summary>
		/// <para>NOT YET IMPLEMENTED.</para>
		/// VertexAndNormalBufferData for Serialization purposes.  Only set if CaptureVBOData is true.
		/// <para>Rhino.FileIO has methods for reading and writing ByteArrays</para>
		/// </summary>
		public byte[] VertexAndNormalBufferData { get; private set; }

		/// <summary>
		/// <para>NOT YET IMPLEMENTED.</para>
		/// IndexBufferData for Serialization purposes.  Only set if CaptureVBOData is true.
		/// <para>Rhino.FileIO has methods for reading and writing ByteArrays</para>
		/// </summary>
		public byte[] IndexBufferData { get; private set; }

		/// <value> The bounding box associated with this mesh. </value>
		public BoundingBox BoundingBox { get; private set; }
	
		/// <value> The runtime material associated with this mesh. </value>
		public DisplayMaterial Material { get; set; }

		/// <value> True if this is a closed mesh. </value>
		public bool IsClosed { get; set; }

		/// <value> True if this mesh has vertex normals. </value>
		public bool HasVertexNormals { get; private set; }

		/// <value> True if the mesh has colors associated with it. </value>
		public bool HasVertexColors { get; private set; }

		/// <value> The width of the element (in bytes) in the array. </value>
		public int Stride { get; private set; }

		/// <value> The number of triangles in this mesh. </value>
		public override uint TriangleCount { get; protected set; }

		/// <value> True if the material associated with this display mesh is not transparent. </value>
		public override bool IsOpaque { get { return Material.Transparency <= 0.0; } }

		/// <value> During the load phase, if this object will not fit on the GPU, it should be marked false. </value>
		public bool? WillFitOnGPU { get; set; }

		/// <value> The partition index of this part of the displayMesh. </value>
		public int PartitionIndex { get { return m_partitionIndex; } }
		#endregion

		#region constructors and disposal
		/// <summary>
		/// Initializes a DisplayMesh from a Rhino.Geometry.Mesh.
		/// </summary>
		public DisplayMesh (Mesh mesh, int partitionIndex, DisplayMaterial material, bool shouldCaptureVBOData, Guid fileObjectId)
		{
			Mesh = mesh;
			FileObjectId = fileObjectId;

			Material = material;
			m_partitionIndex = partitionIndex;
			BoundingBox = mesh.GetBoundingBox (true);

			// Check for normals...
			if (mesh.Normals.Count > 0) 
				HasVertexNormals = true;
			else 
				HasVertexNormals = false;

			// Check for colors...
			if (mesh.VertexColors.Count > 0)
				HasVertexColors = true;
			else
				HasVertexColors = false;

			m_initializationFailed = false;

			CaptureVBOData = shouldCaptureVBOData;
			IsClosed = false;

			IsClosed |= mesh.IsClosed;

			if (m_initializationFailed)
				return;

			CaptureVBOData = false;
		}
			
		/// <summary>
		/// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
		/// </summary>
		~ DisplayMesh () { Dispose (false); }

		/// <summary>
		/// Actively reclaims unmanaged resources that this instance uses.
		/// </summary>
		public new void Dispose()
		{
			try {
				Dispose(true);
				GC.SuppressFinalize(this);
			}
			finally {
				base.Dispose ();
			}
		}

		/// <summary>
		/// <para>This method is called with argument true when class user calls Dispose(), while with argument false when
		/// the Garbage Collector invokes the finalizer, or Finalize() method.</para>
		/// <para>You must reclaim all used unmanaged resources in both cases, and can use this chance to call Dispose on disposable fields if the argument is true.</para>
		/// </summary>
		/// <param name="disposing">true if the call comes from the Dispose() method; false if it comes from the Garbage Collector finalizer.</param>
		private new void Dispose (bool disposing)
		{
			// Free unmanaged resources...

			// Free managed resources...but only if called from Dispose
			// (If called from Finalize then the objects might not exist anymore)
			if (disposing) {
				Vertices = null;
				VerticesNormals = null;
				VerticesColors = null;
				VerticesNormalsColors = null;

				Indices = null;
				VertexBufferData = null;
				NormalBufferData = null;
				VertexAndNormalBufferData = null;
				IndexBufferData = null;

				Material = null;
			}	
		}
		#endregion

		#region Create With Mesh
		/// <summary>
		/// Return an array of DisplayMesh objects created from the parameters
		/// </summary>
		public static Object[] CreateWithMesh(Mesh mesh, ObjectAttributes attr, Material material, int materialIndex)
		{
			// If our render material is the default material, modify our material to match the Rhino default material
			if (materialIndex == -1) {
				material.DiffuseColor = System.Drawing.Color.FromKnownColor (System.Drawing.KnownColor.White);
			}

			Rhino.Geometry.Mesh vertexMesh = new Rhino.Geometry.Mesh();
			// create vertex normals if missing
			if (mesh.Normals.Count == 0) {
				vertexMesh.Normals.Clear ();
				vertexMesh.FaceNormals.Clear ();
				vertexMesh.Compact ();
				vertexMesh.Normals.ComputeNormals ();
				mesh = vertexMesh;
			}
				
			try {
				// The minus 3 here is the Paranoid (TM) Constant...
				// Since we don't know how well CreatePartition deals with edge cases, 
				// we give the routine a slightly smaller parameter than the absolutely largest possible
				mesh.CreatePartitions(ushort.MaxValue-3, int.MaxValue-3);
			} catch (Exception ex) {
				Rhino.Runtime.HostUtils.ExceptionReport (ex);
			}

			if (mesh.PartitionCount == 0) {
				System.Diagnostics.Debug.WriteLine ("Unable to create partitions on mesh {0}", attr.ObjectId.ToString());
				return null; //invalid mesh, ignore
			}

			List<DisplayMesh> displayMeshes = new List<DisplayMesh> ();
			displayMeshes.Capacity = mesh.PartitionCount;

			//System.Diagnostics.Debug.WriteLine("Mesh {0} VertexCount: {1},  FaceCount: {2}, Partition has {3} parts.", attr.ObjectId.ToString(), mesh.Vertices.Count, mesh.Faces.Count, mesh.PartitionCount);

			for (int i = 0; i < mesh.PartitionCount; i++) {
				var displayMaterial = new DisplayMaterial (material, materialIndex);
				var newMesh = new DisplayMesh (mesh, i, displayMaterial, mesh.PartitionCount > 1, attr.ObjectId);
				if (newMesh != null) {
					newMesh.GUID = attr.ObjectId;
					newMesh.IsVisible = attr.Visible;
					newMesh.LayerIndex = attr.LayerIndex;
					displayMeshes.Add (newMesh);
				}
			}

			if (displayMeshes.Count > 1) {
				foreach (DisplayMesh me in displayMeshes)
					me.DeleteVBOData();
			}

			if (vertexMesh != null)
				vertexMesh.Dispose ();

			return displayMeshes.ToArray ();
		}
		#endregion

		#region Interleaved Vertex Data
		/// <summary>
		/// These variables are difficult to archive so we restore them when reloading the model
		/// </summary>
		public void RestoreUsingMesh (Mesh mesh, DisplayMaterial newMaterial)
		{
			Material = newMaterial;
			BoundingBox = mesh.GetBoundingBox (true);
		}
		#endregion

		#region Load Data for VBOs
		/// <summary>
		/// MakeVBOs checks the mesh to see which flavor of VBO should be created and then dispatches creation.
		/// </summary>
		public void LoadDataForVBOs (Mesh mesh)
		{
			bool didLoadData = true;

			if (HasVertexColors) {
				if (HasVertexNormals) {
					didLoadData = didLoadData && LoadVertexNormalColorData (mesh, m_partitionIndex);
				} else {
					didLoadData = didLoadData && LoadVertexColorData (mesh, m_partitionIndex);
				}
			} else if (HasVertexNormals) {
				didLoadData = didLoadData && LoadVertexNormalData (mesh, m_partitionIndex);
			} else {
				didLoadData = didLoadData && LoadVertexData (mesh, m_partitionIndex);
			}

			// Every mesh gets an index...
			didLoadData = didLoadData && LoadIndexData (mesh, m_partitionIndex);

			m_initializationFailed = !didLoadData;
		}

		/// <summary>
		/// Loads vertex data from a mesh given a partitionIndex.
		/// </summary>
		protected bool LoadVertexData (Mesh mesh, int partitionIndex) 
		{
			Stride = sizeof(float) * 3; // 12 bytes

			MeshPart meshPartition = mesh.GetPartition (partitionIndex);
			int vertexCount = meshPartition.VertexCount;
			int startIndex = meshPartition.StartVertexIndex;
			int endIndex = meshPartition.EndVertexIndex;

			int count = endIndex - startIndex;

			var vertices = new VData[count];

			for (int i = startIndex; i < endIndex; i++)
				vertices [i - startIndex].Vertex = mesh.Vertices [i];

			Vertices = vertices;

			TriangleCount += (uint)mesh.Faces.Count;

			if (Vertices.Length > 0)
				return true;
			else 
				return false;
		}

		/// <summary>
		/// Loads vertex and normal data from a mesh given a partition Index
		/// </summary>
		protected bool LoadVertexNormalData (Mesh mesh, int partitionIndex)
		{
			Stride = (Marshal.SizeOf (typeof(VNData))); // 24 bytes

			MeshPart meshPartition = mesh.GetPartition (partitionIndex);
			int vertexCount = meshPartition.VertexCount;
			int startIndex = meshPartition.StartVertexIndex;
			int endIndex = meshPartition.EndVertexIndex;

			int count = (endIndex - startIndex) * 2;

			var verticesNormals = new VNData[count];

			for (int i = startIndex; i < endIndex; i++) {
				verticesNormals [i-startIndex].Vertex = mesh.Vertices [i];
				verticesNormals [i-startIndex].Normal = mesh.Normals [i];
			}

			VerticesNormals = verticesNormals;

			TriangleCount += (uint)mesh.Faces.Count;

			return VerticesNormals.Length > 0;
		}

		/// <summary>
		/// Loads vertex and color data from a mesh, given a partition index
		/// </summary>
		protected bool LoadVertexColorData (Mesh mesh, int partitionIndex)
		{
			Stride = (Marshal.SizeOf (typeof(VCData))); // 28 bytes

			MeshPart meshPartition = mesh.GetPartition (partitionIndex);
			int vertexCount = meshPartition.VertexCount;
			int startIndex = meshPartition.StartVertexIndex;
			int endIndex = meshPartition.EndVertexIndex;

			int count = (endIndex - startIndex) * 2;

			var verticesColors = new VCData[count];

			for (int i = startIndex; i < endIndex; i++) {
				verticesColors [i-startIndex].Vertex = mesh.Vertices [i];
				verticesColors [i-startIndex].Color  = new Color4f (mesh.VertexColors [i]);
			}

			VerticesColors = verticesColors;

			TriangleCount += (uint)mesh.Faces.Count;

			return VerticesColors.Length > 0;
		}

		/// <summary>
		/// Loads vertex, normal, and color data from a mesh, given a partition index
		/// </summary>
		protected bool LoadVertexNormalColorData (Mesh mesh, int partitionIndex)
		{
			Stride = (Marshal.SizeOf (typeof(VNCData))); // This should be 40 bytes

			MeshPart meshPartition = mesh.GetPartition (partitionIndex);
			int vertexCount = meshPartition.VertexCount;
			int startIndex = meshPartition.StartVertexIndex;
			int endIndex = meshPartition.EndVertexIndex;

			int count = (endIndex - startIndex) * 3;

			VNCData[] verticesNormalsColors = new VNCData[count];

			for (int i = startIndex; i < endIndex; i++) {
				verticesNormalsColors [i-startIndex].Vertex = mesh.Vertices [i];
				verticesNormalsColors [i-startIndex].Normal = mesh.Normals [i];
				verticesNormalsColors [i-startIndex].Color  = new Color4f (mesh.VertexColors [i]);
			}

			VerticesNormalsColors = verticesNormalsColors;

			TriangleCount += (uint)mesh.Faces.Count;

			return VerticesNormalsColors.Length > 0;
		}

		/// <summary>
		/// Loads index data from a mesh, given a partitionIndex
		/// </summary>
		protected bool LoadIndexData (Mesh mesh, int partitionIndex)
		{
			MeshPart part = mesh.GetPartition (partitionIndex);
			TriangleCount = (uint)part.TriangleCount;
			var indexList = new List<ushort> ();

			for (int fi = part.StartFaceIndex; fi < part.EndFaceIndex; fi++) {
				var f = mesh.Faces [fi];
			
				int i0 = f.A; int i1 = f.B; int i2 = f.C; int i3 = f.D;

				indexList.Add ((ushort)(i0 - part.StartVertexIndex));
				indexList.Add ((ushort)(i1 - part.StartVertexIndex));
				indexList.Add ((ushort)(i2 - part.StartVertexIndex));
				if (i2 != i3) {
					indexList.Add ((ushort)(i2 - part.StartVertexIndex));
					indexList.Add ((ushort)(i3 - part.StartVertexIndex));
					indexList.Add ((ushort)(i0 - part.StartVertexIndex));
				}
			}

			Indices = indexList.ToArray ();
			IndexBufferLength = Indices.Length;
			indexList.Clear ();
		
			return Indices.Length > 0;
		}


		#endregion

		#region VBO Management
		/// <summary>
		/// Sets all VBO data to null.
		/// </summary>
		public void DeleteVBOData ()
		{
			Vertices = null;
			VerticesNormals = null;
			VerticesColors = null;
			VerticesNormalsColors = null;
		}

		/// <summary>
		/// Sets the Index Buffer Objects to null
		/// </summary>
		public void DeleteIndexBufferObject ()
		{
			Indices = null;
		}
		#endregion

		#region Utilities
		/// <summary>
		/// Converts Object to a Byte Array for Serialization
		/// </summary>
		protected byte[] ObjectToByteArray(Object obj)
		{
			if(obj == null)
				return null;
			BinaryFormatter binaryFormatter = new BinaryFormatter();

			using (MemoryStream memoryStream = new MemoryStream()) { //eagerly release the internal stream...
				try {
					binaryFormatter.Serialize(memoryStream, obj);
					return memoryStream.ToArray();
				} catch (System.Runtime.Serialization.SerializationException ex) {
					System.Diagnostics.Debug.WriteLine ("WARNING: Could not serialize the object with exception: {0}", ex.Message);
					Rhino.Runtime.HostUtils.ExceptionReport (ex);
					memoryStream.Close ();
					return null;
				}
			}
		}
		#endregion
	}
}