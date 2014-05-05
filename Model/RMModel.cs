//
// RMModel.cs
// RhinoMobile.Model
//
// Created by dan (dan@mcneel.com) on 9/19/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Collections;
using RhinoMobile.Display;
using System.Threading.Tasks;
using System.Threading;

#if __IOS__
using MonoTouch.Foundation;
#endif

#if __ANDROID__
using Android;
#endif

namespace RhinoMobile.Model
{
	public class RMModel : IDisposable
	{
		#region Members
		protected string m_modelID;
		protected string m_modelPath;
		protected BoundingBox m_visibleLayersBoundingBox;
		protected BoundingBox m_bBox;
		protected ViewInfo m_defaultView;

		protected CancellationTokenSource m_cancellation_token_source;
		public event MeshPreparationHandler MeshPrep;
		protected MeshPreparationProgress m_meshPrepProgress;
		#endregion

		#region Properties
		/// <value> The ID can be used in a database for model lookup in a table. </value> 
		public virtual int Id { get; set; }

		/// <value> The SortOrder can be used to save position in a sort in a database. </value>
		public virtual int SortOrder { get; set; }

		/// <value> Model title (as displayed to the user, without the .3dm extension). </value>
		public virtual string Title { get; set; }

		/// <remarks>
		/// Setting the modelID invalidates any cached data. If the modelID has changed, 
		/// the user likely modified a file in the Documents folder.
		/// </remarks>
		public virtual string ModelID { 
			get {
				return m_modelID;
			}

			set {
				DeleteCaches ();
				m_modelID = value;
			}
		}

		/// <value>
		/// <para>Android: all mesh and thumbnail data is stored in this directory: data/data/[AppName]/files/appsupport.</para>
		/// <para>iOS: all mesh and thumbnail data is stored in this directory: ~/Library/Application Support/. </para> 
		/// </value>
		public virtual string SupportDirectoryName { get; set; }

		/// <value>
		/// <para>iOS Notes: Downloaded model contents are stored in this file in our Documents directory.  
		/// The Documents directory is visible in the File Sharing section of iTunes, so it is possible for the user to delete this file.
		/// The Documents directory is also automagically backed up by iCloud as of iOS 5.1 </para>
		/// </value>
		public virtual string DocumentsFilename { get; set; }

		/// <value> True if model file exists in Documents path. </value>
		public virtual bool Downloaded { get; set; }

		/// <value> iOS: resource name if the model is available in the application bundle (used for sample files) </value>
		public virtual string BundleName { get; set; }

		/// <value> The 3dm file on disk associated with this instance. </value>
		public virtual File3dm ModelFile { get; set; }

		/// <value>
		/// Returns the full pathname of 3dm file associated with this model.  
		/// This is a combination of the platform-specific path and the 3dm file's name:
		/// Android: /data/data/[AppName]/files
		/// iOS: ./Documents/filename.3dm
		/// See also: DocumentsFilename
		/// </value>
		public virtual string ModelPath { 
			get {
				if (!string.IsNullOrEmpty(DocumentsFilename)) {
					var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
					var fullPath = Path.Combine (documentsPath, DocumentsFilename);
					m_modelPath = fullPath;
				} else m_modelPath = string.Empty;

				return m_modelPath;
			}

			protected set {
				m_modelPath = value;
			}
		}

		/// <value> List of ModelObjects.  These are used in the creation of DisplayObjects, then disposed of. </value> 
		protected virtual List<ModelObject> ModelObjects { get; set; }

		/// <value> DisplayObjects is a list of all the objects to be displayed. </value>
		public virtual List<DisplayObject> DisplayObjects { get; protected set; }

		/// <value> TransparentObjects is a list of all DisplayObjects that are not opaque. </value>
		public virtual List<DisplayObject> TransparentObjects { get; protected set; }

		/// <value> Dictionary of ModelObjects, with Guid keys. </value>
		protected virtual Dictionary<Guid, ModelObject> ModelObjectsDictionary { get; set; }

		/// <value> All the meshes that were found or derived from the 3dm file. </value>
		public virtual RhinoList<Mesh> AllMeshes { get; protected set; }

		/// <value> Returns a BoundingBox for all geometry on visible layers. </value>
		public virtual BoundingBox VisibleLayersBBox
		{
			get {
				if (ModelFile != null) {
					m_visibleLayersBoundingBox = BoundingBox.Empty;

					for (int layerIndex = 0; layerIndex < Layers.Count; layerIndex++) {
						if (LayerIsVisibleAtIndex (layerIndex)) {
							File3dmObject[] objsByLayer = ModelFile.Objects.FindByLayer(LayerAtIndex (layerIndex).Name);

							foreach (File3dmObject obj in objsByLayer)
								m_visibleLayersBoundingBox.Union (obj.Geometry.GetBoundingBox (false));
						}
					}
				} else {
					return BoundingBox.Empty;
				}

				return m_visibleLayersBoundingBox;
			}

			set {
				m_visibleLayersBoundingBox = value;
			}
		}

		/// <value> The Bounding Box surrounding the model.  Returns an empty bounding box if the model is empty. </value>
		public virtual BoundingBox BBox
		{
			get {
				if (m_bBox.IsValid)
					return m_bBox;
				return BoundingBox.Empty;
			}

			protected set {
				m_bBox = value;
			}
		}

		/// <value> Contains the bounding box for all objects in each layer. </value>
		protected virtual RhinoList<BoundingBox> LayerBBoxes { get; set; }

		/// <value> The name of the model, without the file extension. </value>
		protected virtual string BaseName
		{
			get {
				return Title;
			}
		}

		/// <value> Sum of the DisplayObjects and the TransparentObjects; i.e.: the total number of valid meshes. </value>
		public virtual long MeshCount 
		{ 
			get { return DisplayObjects.Count + TransparentObjects.Count; }
		}

		/// <value> The polygon count of all the displayed and transparent objects in the entire model. </value>
		public virtual uint PolygonCount 
		{ 
			get {
				uint triangles = 0;
			
				foreach (DisplayObject me in DisplayObjects)
					triangles += me.TriangleCount;
				foreach (DisplayObject me in TransparentObjects)
					triangles += me.TriangleCount;

				return triangles;
			}
		}

		/// <value> The total count of all displayable geometry. </value>
		public virtual long GeometryCount { get; protected set; }

		/// <value> The total count of the BRep objects in the 3dm file. </value>
		public virtual long BRepCount { get; protected set; }

		/// <value> The total count of the BReps that have a mesh attached to them. </value>
		public virtual long BRepWithMeshCount { get; protected set; }

		/// <value> The total count of the Extrusion Objects in the model. </value>
		public virtual long ExtrusionCount { get; protected set; }

		/// <value> The layers in this model. </value>
		public virtual List<Layer> Layers { get; protected set; }

		/// <value> The layers in this model that contain geometry. </value>
		public virtual List<Layer> LayersWithGeometry { get; protected set; }

		/// <value> True if the 3dm file was read successfully; false if there was an error reading the 3dm file. </value>
		public virtual bool ReadSuccessfully { get; protected set; }

		/// <value> True if the model is ready to be rendered to the screen. </value>
		public virtual bool IsReadyForRendering { get; protected set; }

		/// <value> Set to true if there was an error in mesh preparation. </value>
		public virtual bool InitializationFailed { get; set; }

		/// <value> True if the mesh initialization was cancelled. </value>
		public virtual bool PreparationCancelled { get; protected set; }

		/// <value> Set to true if there was a memory warning (iOS). </value>
		public virtual bool OutOfMemoryWarning { get; set; }

		/// <value> True if there are DisplayObjects and TransparentObjects OR the Initialized did not fail </value>
		public virtual bool MeshesInitialized 
		{
			get { return (DisplayObjects != null && TransparentObjects != null) || !InitializationFailed; }
		}

		/// <summary>
		/// The default view to be returned to on a zoom-extensions action.
		/// </summary>
		public virtual ViewInfo DefaultView 
		{ 
			// NOTE: Not an auto property because we are calling ref on the backing m_defaultView member variable
			get { return m_defaultView; }
			protected set { m_defaultView = value; }
		}
		#endregion

		#region Constructors and Disposal
		public RMModel ()
		{

		}

		/// <summary>
		/// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
		/// </summary>
		~RMModel() { Dispose (false); }

		/// <summary>
		/// Actively reclaims unmanaged resources that this instance uses.
		/// </summary>
		public void Dispose()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
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
			// Free unmanaged resources...

			// Free managed resources...but only if call from Dispose
			// (If called from Finalize then the objects might not exist anymore)
			if (disposing) {
				DisposeOfModelData ();
			}
		}

		public void DisposeOfModelData ()
		{
			if (ModelFile != null) {
				ModelFile.Dispose ();
				ModelFile = null;
			}

			if (DisplayObjects != null) {
				DisplayObjects.Clear ();
				DisplayObjects = null;
			}

			if (TransparentObjects != null) {
				TransparentObjects.Clear ();
				TransparentObjects = null;
			}

			if (ModelObjectsDictionary != null) {
				ModelObjectsDictionary.Clear ();
				ModelObjectsDictionary = null;
			}

			if (ModelObjects != null) {
				ModelObjects.Clear ();
				ModelObjects = null;
			}

			if (DefaultView != null) {
				DefaultView.Dispose ();
				DefaultView = null;
			}

			if (AllMeshes != null) {
				AllMeshes.Clear ();
				AllMeshes = null;
			}

			if (Layers != null) {
				Layers.Clear ();
				Layers = null;
			}

			if (LayersWithGeometry != null) {
				LayersWithGeometry.Clear ();
				LayersWithGeometry = null;
			}

			if (LayerBBoxes != null) {
				LayerBBoxes.Clear ();
				LayerBBoxes = null;
			}

			VisibleLayersBBox = BoundingBox.Empty;
			BBox = BoundingBox.Empty;

			m_visibleLayersBoundingBox = BoundingBox.Empty;
			m_bBox = BoundingBox.Empty;
			m_meshPrepProgress = null;

			IsReadyForRendering = false;
		}
		#endregion

		#region Prepare Model
		/// <summary>
		/// Calling this method starts the preparation process for readying a RMModel for
		/// drawing to the screen.  When this process is completed successfully,
		/// IsReadyForRendering is set to true.
		/// </summary>
		public virtual async void Prepare ()
		{
			if (Downloaded) {
				// Check the revision history of the file and set the modelID
				ModelID = InspectRevisionHistory (ModelPath);

				if (ModelFile == null) {
					DisplayObjects = new List<DisplayObject> ();
					TransparentObjects = new List<DisplayObject> ();
					ModelObjects = new List<ModelObject> ();
					ModelObjectsDictionary = new Dictionary<Guid, ModelObject> ();
					BRepCount = 0;
					BRepWithMeshCount = 0;

					// Get the file size
					int fileSize = Convert.ToInt32 (GetFileSize ());
				
					// Read the 3dm file with an ObjectTypeFilter...
					string errorLog;
					ModelFile = File3dm.ReadWithLog (ModelPath, out errorLog);

					// Check to make sure the 3dm was read correctly and there were no errors...
					bool didOpenFile = false;
					if ((ModelFile != null) && (errorLog == string.Empty))
						didOpenFile = true;

					// Preparation Dispatch...
					if (didOpenFile) {
						PrepareLayers ();
						PrepareBoundingBoxes ();
						PrepareViewports ();

						//Prepare Meshes async
						Int64 result = 0;
						Progress<Int64> progress = new  Progress<Int64>();
						m_cancellation_token_source = new CancellationTokenSource();

						//FOR DEBUGGING ONLY...
						//PrepareMeshesSync (progress, m_cancellation_token_source.Token);

						try
						{
							result = await PrepareMeshesAsync (progress, m_cancellation_token_source.Token);
						}
						catch (OperationCanceledException ex)
						{
							MeshPreparationDidFailWithException (MeshException ("Initialization cancelled."));
							Rhino.Runtime.HostUtils.ExceptionReport (ex);
							Dispose ();
							return;
						}
					}
				}
			}

			IsReadyForRendering |= ReadSuccessfully;
		}

		/// <summary>
		/// We construct a modelID string from the Revision History objects with the hope
		/// that this will uniquely identify the file contents.  If the user changes
		/// the file contents, we should be able to detect this because the constructed modelID
		/// string will also change.
		/// </summary>
		protected virtual string InspectRevisionHistory (string path)
		{
			string identifier = string.Empty;

			try
			{
				string createdBy = string.Empty;
				string lastEditedBy = string.Empty;
				int revision = 0;
				DateTime createdOn = new DateTime();
				DateTime lastEditedOn = new DateTime();
				File3dm.ReadRevisionHistory(path, out createdBy, out lastEditedBy, out revision, out createdOn, out lastEditedOn);
				identifier = createdBy + lastEditedBy + revision.ToString () + createdOn.ToString() + lastEditedOn.ToString ();
			} catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine (ex.Message);
				Rhino.Runtime.HostUtils.ExceptionReport (ex);
				return identifier;
			}

			return identifier;
		}

		/// <summary>
		/// Cancels the model preparation
		/// </summary>
		public virtual void CancelModelPreparation () 
		{
			PreparationCancelled = true;
			if (m_cancellation_token_source != null)
				m_cancellation_token_source.Cancel ();
		}
		#endregion

		#region Layers
		/// <summary>
		/// Prepares the Layers in the ModelFile
		/// </summary>
		protected virtual void PrepareLayers ()
		{
			if (ModelFile != null) {
			
				// Set the Layers property on this model
				Layers = new List<Layer> ();
				foreach (Layer layer in ModelFile.Layers)
					Layers.Add (layer);
	
				// Find the Layers with Geometry and add them to LayersWithGeometry property
				LayersWithGeometry = new List<Layer> ();
				for  (int i = 0; i < Layers.Count; i++) {
					if (LayerHasGeometryAtIndex (i))
						LayersWithGeometry.Add (Layers [i]);
				}

			}
		}

		/// <summary>
		/// Returns the layer at the index provided, provided the layer exists, else returns null
		/// </summary>
		public virtual Layer LayerAtIndex(int layerIndex) 
		{
			if (layerIndex >= 0 && layerIndex < ModelFile.Layers.Count) {
				if (ModelFile != null)
					return ModelFile.Layers [layerIndex];
			}
			System.Diagnostics.Debug.WriteLine ("Bad layer index: %", layerIndex);
			return null; 
		}

		/// <summary>
		/// Returns the layer containing geometry at the index provided.
		/// </summary>
		public virtual Layer LayerWithGeometryAtIndex(int layerIndex) 
		{
			for (int i = 0; i < ModelFile.Layers.Count; i++) {
				if (LayerHasGeometryAtIndex(i)) {
					if (layerIndex == 0)
						return ModelFile.Layers [i];
					layerIndex--;
				}
			}

			return null;
		}

		/// <summary>
		/// True if the layer at the index provided is visible.
		/// </summary>
		public virtual bool LayerIsVisibleAtIndex(int layerIndex) 
		{
			if (layerIndex >= 0 && layerIndex < ModelFile.Layers.Count) {
				Layer layer = ModelFile.Layers[layerIndex];
				return layer.IsVisible;
			}
			System.Diagnostics.Debug.WriteLine ("Bad Layer Index: {0}", layerIndex);
			return false; //punt
		}

		/// <summary>
		/// True if layer at the index provided contains geometry.
		/// </summary>
		public virtual bool LayerHasGeometryAtIndex(int layerIndex) 
		{
			if (layerIndex >= 0 && layerIndex < Layers.Count) {
				BoundingBox bb = BoundingBoxForLayer (layerIndex);
				return bb.IsValid;
			}
			System.Diagnostics.Debug.WriteLine ("Bad Layer Index: {0}", layerIndex);
			return false; // punt
		}

		/// <summary>
		/// Returns the bounding box for all geometry on a layer.
		/// </summary>
		protected virtual BoundingBox BoundingBoxForLayer(int layerIndex)
		{
			BoundingBox boundingBoxForLayer = BoundingBox.Empty;

			if (ModelFile != null) {
				File3dmObject[] objsByLayer = ModelFile.Objects.FindByLayer (LayerAtIndex (layerIndex).Name);

				foreach (File3dmObject obj in objsByLayer)
					boundingBoxForLayer.Union (obj.Geometry.GetBoundingBox (false));
			} 

			return boundingBoxForLayer;
		}
		#endregion

		#region Bounding Boxes
		/// <summary>
		/// Prepare the Bounding Boxes in the ModelFile
		/// </summary>
		protected virtual void PrepareBoundingBoxes()
		{
			if (ModelFile != null) {
				// Prepare BBoxes
				// Get the entire model's bounding box
				BBox = ModelFile.Objects.GetBoundingBox ();

				// Calculate the layerBBoxes
				LayerBBoxes = new RhinoList<Rhino.Geometry.BoundingBox> ();
				for (int layerIndex = 0; layerIndex < Layers.Count; layerIndex++) {
					File3dmObject[] objsByLayer = ModelFile.Objects.FindByLayer (LayerAtIndex (layerIndex).Name);
					BoundingBox bbox = new BoundingBox ();

					foreach (File3dmObject obj in objsByLayer) {
						bbox.Union (obj.Geometry.GetBoundingBox (false));
					}

					LayerBBoxes.Insert (layerIndex, bbox);
				}
			}
		}
	
		/// <summary>
		/// Unions the entire ObjectTable BBox with the geo BBox and, if layerIndex is less than the count of layerBBox, 
		/// unions the LayerBBoxes list at the layerIndex provided with the geo BBox.
		/// </summary>
		protected virtual void AddObjectBoundingBox (GeometryBase geo, int layerIndex) 
		{
			ModelFile.Objects.GetBoundingBox().Union (geo.GetBoundingBox (false));

			if ((layerIndex >= 0) && (layerIndex < LayerBBoxes.Count ()))
				LayerBBoxes [layerIndex].Union (geo.GetBoundingBox (false));	                                          
		}
		#endregion

		#region Viewports
		/// <summary>
		/// Prepare the Viewports in the ModelFile
		/// </summary>
		protected virtual void PrepareViewports()
		{
			if (ModelFile != null) {
				// Initialize DefaultView from model
				bool initialized = false;
				int view_count = ModelFile.Views.Count;

				if (view_count > 0) {
					// find first perspective viewport projection in file
					for (int i = 0; i < view_count; i++) {
						if (ModelFile.Views [i].Viewport.IsPerspectiveProjection) {
							initialized = true;
							DefaultView = ModelFile.Views [i];
							DefaultView.Viewport.TargetPoint = ModelFile.Views [i].Viewport.TargetPoint;
							DefaultView.Name = ModelFile.Views [i].Name;
						}
					}
				}

				// If there were no perspective views, make one...
				if (!initialized) {
					DefaultView = ModelFile.Views [0];
					GetDefaultView (BBox, ref m_defaultView);
				}

				// fix up viewport values
				Rhino.Geometry.Vector3d camDir = DefaultView.Viewport.CameraDirection;
				camDir.Unitize ();
				DefaultView.Viewport.SetCameraDirection (camDir);

				Rhino.Geometry.Vector3d camUp = DefaultView.Viewport.CameraUp;
				camUp.Unitize ();
				DefaultView.Viewport.SetCameraUp (camUp);
			}
		}

		/// <summary>
		/// Sets the DefaultView given the boundingBox and the viewInfo
		/// </summary>
		public virtual void GetDefaultView (BoundingBox bbox, ref ViewInfo view) {
			// simple parallel projection of bounding box;
			const double window_height = 1.0;
			const double window_width = 1.0;
			double dx, dy, dz;
			double frus_near, frus_far;
			Point3d camLoc;
			Vector3d camDir, camUp;

			view.Viewport.TargetPoint = 0.5*(bbox.Min + bbox.Max);
			dx = 1.1*(bbox.Max[0] - bbox.Min[0]);
			dy = 1.1*(bbox.Max[1] - bbox.Min[1]);
			dz = 1.1*(bbox.Max[2] - bbox.Min[2]);
			if ( dx <= 1.0e-6 && dy <= 1.0e-6 )
				dx = dy = 2.0;
			if ( window_height*dx < window_width*dy ) {
				dx = dy*window_width/window_height;
			}
			else {
				dy = dx*window_height/window_width;
			}
			if ( dz <= 0.1*(dx+dy) )
				dz = 0.1*(dx+dy);
			dx *= 0.5;
			dy *= 0.5;
			dz *= 0.5;

			frus_near = 1.0;
			frus_far = frus_near + 2.0*dz;
			camLoc = view.Viewport.TargetPoint + (dz + frus_near)*Vector3d.ZAxis;
			camDir = new Vector3f(0, 0, -1);
			camUp = Vector3f.YAxis;

			view.Viewport.ChangeToParallelProjection (false);
			view.Viewport.SetCameraLocation (camLoc);
			view.Viewport.SetCameraDirection (camDir);
			view.Viewport.SetCameraUp (camUp);
			view.Viewport.SetFrustum ( -dx, dx, -dy, dy, frus_near, frus_far );
		}
		#endregion

		#region Meshes
		/// <summary>
		/// Prepare meshes on a separate thread, supporting cancellation and reporting of progress.
		/// </summary>
		/// <param name="progress">Optional class to report progress. Pass null if you don't care to receive progress.</param>
		/// <param name="cancellationToken">Optional CancellationToken. Pass CancellationToken.None if you don't care to cancel.</param>
		protected virtual Task<Int64> PrepareMeshesAsync (IProgress<Int64> progress, CancellationToken cancellationToken)
		{
			return Task.Run(
				() => PrepareMeshes(progress, cancellationToken)
				);
		}

		/// <summary>
		/// FOR DEBUGGING ONLY...the synchronous version of PrepareMeshes
		/// </summary>
		protected virtual void PrepareMeshesSync (IProgress<Int64> progress, CancellationToken cancellationToken)
		{
			PrepareMeshes (progress, cancellationToken);
		}

		/// <summary>
		/// The PrepareMeshes task...
		/// </summary>
		protected virtual Int64 PrepareMeshes (IProgress<Int64> progress, CancellationToken cancellationToken)
		{
			Int64 tally = 0;

			Exception prepareMeshesException = MeshException (string.Empty);
			// Show we have started reading the meshes
			MeshPreparationProgressEvent (tally);

			if (Downloaded) {
				bool successfulPreparation = true;
				if (successfulPreparation) {
					// Prepare each object in the 3dm file...
					for (Int64 i = 0; i < ModelFile.Objects.Count; i++) {
						PrepareObject (ModelFile.Objects [(int)i].Geometry, ModelFile.Objects [(int)i].Attributes);

						tally++;
						if (progress != null)
							MeshPreparationProgressEvent ((float)i / (float)ModelFile.Objects.Count);
						if (cancellationToken.IsCancellationRequested)
							throw new OperationCanceledException();
					}

					int count = ModelObjects.Count;

					// add instance definitions to our object dictionary
					foreach (InstanceDefinitionGeometry instanceDef in ModelFile.InstanceDefinitions) {
						ModelInstanceDef iDef = new ModelInstanceDef (instanceDef);
						ModelObjectsDictionary.Add (iDef.ObjectId, iDef);
					}
	
					// explode all the instance refs into DisplayMesh and DisplayInstanceMesh objects
					var identity = Transform.Identity;
					List<DisplayObject> explodedObjects = new List<DisplayObject>();

					// Sort by object type: ModelMesh, ModelInstanceRef
					foreach (ModelMesh modelObject in ModelObjects.OfType<ModelMesh>())
						modelObject.ExplodeIntoArray (this, explodedObjects, identity);

					foreach (ModelInstanceRef modelObject in ModelObjects.OfType<ModelInstanceRef>())
						(modelObject as ModelInstanceRef).ExplodeIntoArray (this, explodedObjects, identity);

					// split explodedObjects into displayObjects and transparentObjects
					foreach (DisplayObject obj in explodedObjects) {

						if (obj.GetType () == Type.GetType ("RhinoMobile.Display.DisplayMesh")) {
							if ((obj as DisplayMesh).IsOpaque) {
								DisplayObjects.Add (obj);
							} else {
								TransparentObjects.Add (obj);
							}
						}

						if (obj.GetType () == Type.GetType ("RhinoMobile.Display.DisplayInstanceMesh")) {
							if ((obj as DisplayInstanceMesh).IsOpaque) {
								DisplayObjects.Add (obj);
							} else {
								TransparentObjects.Add (obj);
							}
						}
					}

					// we are done with the model objects
					ModelObjects.Clear ();
					ModelObjectsDictionary.Clear ();

					// look for models that cannot be displayed
					if ((DisplayObjects.Count == 0) && (TransparentObjects.Count == 0)) {
						if ((BRepCount > 0) && (BRepWithMeshCount == 0)) {
							prepareMeshesException = MeshException ("This model is only wireframes and cannot be displayed.  Save the model in shaded mode and download again.");
						} else if ((GeometryCount > 0) && (BRepWithMeshCount == 0)) {
							prepareMeshesException = MeshException ("This model has no renderable geometry.");
						} else if (GeometryCount == 0 && BRepWithMeshCount == 0) {
							prepareMeshesException = MeshException ("This model is empty.");
						} else {
							successfulPreparation = false;
						}
					}

					if (!successfulPreparation) {
						DisplayObjects.Clear ();
						TransparentObjects.Clear ();
						ModelFile.Dispose ();

						if (OutOfMemoryWarning) {
							prepareMeshesException = MeshException ("Model is too large.");
						} else if (PreparationCancelled) {
							prepareMeshesException = MeshException ("Initialization cancelled.");
						} else {
							prepareMeshesException = MeshException ("This model is corrupt and cannot be displayed.");
						}
					}

					ReadSuccessfully = successfulPreparation;
				}

				if (prepareMeshesException.Message != string.Empty) {
					System.Diagnostics.Debug.WriteLine (prepareMeshesException.Message);
					MeshPreparationDidFailWithException (prepareMeshesException);
				}

				IsReadyForRendering |= ReadSuccessfully;
				if (IsReadyForRendering)
					MeshPreparationDidSucceed ();
			}

			return tally;
		}

		/// <summary>
		/// We create a DisplayMesh object for each render mesh object we find.  If we encounter an Mesh object,
		/// we create a DisplayMesh object from the Mesh. For any Brep objects, we create Displaymesh objects
		/// from the render mesh.  For any Extrusion objects, we create a DisplayMesh from the RenderMesh.  For
		/// InstanceReference objects, _____
		/// </summary>
		protected virtual void PrepareObject (GeometryBase pObject, ObjectAttributes attr)
		{		
			while (LayerBBoxes.Count < ModelFile.Layers.Count) {
				BoundingBox invalidBBox = BoundingBox.Empty;
				LayerBBoxes.Add (invalidBBox);
			}

			// Geometry sorting...
			// Meshes--------------------------------------------------------------------------------------------
			if (pObject.ObjectType == ObjectType.Mesh /*&& attr.Mode != ObjectMode.InstanceDefinitionObject*/) {
				if (!(pObject as Mesh).IsValid) {
					(pObject as Mesh).Compact ();
					(pObject as Mesh).Normals.ComputeNormals ();
				}

				// Check to see if any of the vertices are hidden...if they are, remove them from the list
				for (int i = 0; i < (pObject as Mesh).Vertices.Count; i++) {
					bool vertexIsHidden = (pObject as Mesh).Vertices.IsHidden (i);
					if (vertexIsHidden) {
						(pObject as Mesh).Vertices.Remove (i, true);
					}
				}

				// Some 3dm files have meshes with no normals and this messes up the shading code...
				if (((pObject as Mesh).Normals.Count == 0) &&
				    ((pObject as Mesh).Vertices.Count > 0) &&
				    ((pObject as Mesh).Faces.Count > 0)) {
					(pObject as Mesh).Normals.ComputeNormals ();
				} 	

				GeometryCount++;
		
				AddModelMesh ((pObject as Mesh), attr);
				// Breps -------------------------------------------------------------------------------------------
			} else if (pObject.ObjectType == ObjectType.Brep) {
				BRepCount++;
				List<Mesh> meshes = new List<Mesh> ();

				//Loop through each BrepFace in the BRep and GetMesh, adding it to meshes
				int count = 0;
				foreach (BrepFace face in (pObject as Brep).Faces) {
					var renderMesh = face.GetMesh (MeshType.Render);
					if (renderMesh != null) {
						meshes.Add (face.GetMesh (MeshType.Render));
						count++;
					}
				}

				if (count > 0)
					BRepWithMeshCount++;
				if (count == 1) {
					if ((meshes [0] != null) && (meshes [0].Vertices.Count > 0)) {
						GeometryCount++;

						AddModelMesh (meshes [0], attr);
					}
				} else if (count > 0) {
					Mesh meshCandidate = new Mesh ();

					// Sometimes it's possible to have lists of NULL ON_Meshes...Rhino
					// can put them there as placeholders for badly formed/meshed breps.
					// Therefore, we need to always make sure we're actually looking at
					// a mesh that contains something and/or is not NULL.
					for (int i = 0; i < count; i++) {
						// If we have a valid pointer, append the mesh to our accumulator mesh...
						if (meshes [i] != null) {
							meshCandidate.Append (meshes [i]);
						}
					}

					// See if the end result actually contains anything and add it to our model if it does...
					if (meshCandidate.Vertices.Count > 0) {
						GeometryCount++;
						AddModelMesh (meshCandidate, attr);
					}
				}
				// Extrusions --------------------------------------------------------------------------------------
			} else if (pObject.ObjectType == ObjectType.Extrusion) { 
				ExtrusionCount++;

				Rhino.Geometry.Mesh meshCandidate = (pObject as Extrusion).GetMesh (Rhino.Geometry.MeshType.Render);

				// See if the end result actually contains anything and add it to our model if it does...
				if (meshCandidate != null) {
					if (meshCandidate.Vertices.Count > 0) {
						GeometryCount++;

						AddModelMesh(meshCandidate, attr);
					}
				}
				// Instance References -----------------------------------------------------------------------------
			} else if (pObject.ObjectType == ObjectType.InstanceReference) {
				Rhino.Geometry.InstanceReferenceGeometry instanceRef = (InstanceReferenceGeometry)pObject;
				ModelInstanceRef iRef = new ModelInstanceRef(attr.ObjectId, instanceRef.ParentIdefId, instanceRef.Xform);

				if (iRef != null) {
					iRef.LayerIndex = attr.LayerIndex;
					GeometryCount++;

					AddModelObject (iRef, attr);
				}
			} 

		}

		/// <summary>
		/// Adds a ModelObject to the ModelObjects List
		/// </summary>
		protected virtual void AddModelObject (ModelObject obj, ObjectAttributes attr)
		{
			if (obj != null) {
				Material material = new Material ();
		
				int materialIndex = -1;

				switch (attr.MaterialSource) {
				case (ObjectMaterialSource.MaterialFromLayer):
					if (attr.LayerIndex >= 0 && attr.LayerIndex < ModelFile.Layers.Count)
						materialIndex = ModelFile.Layers [attr.LayerIndex].RenderMaterialIndex;
					break;
				case (ObjectMaterialSource.MaterialFromObject):
					materialIndex = attr.MaterialIndex;
					break;
				case (ObjectMaterialSource.MaterialFromParent):
					materialIndex = attr.MaterialIndex;
					break;
				}

				if (materialIndex < 0 || materialIndex >= ModelFile.Materials.Count) {
					materialIndex = -1;
					material.Default ();
				} else {
					material = ModelFile.Materials [materialIndex];
				}

				obj.Material = material;
				obj.LayerIndex = attr.LayerIndex;
				obj.Visible = attr.Visible;

				ModelObjectsDictionary.Add (obj.ObjectId, obj);

				// If the object is not an instance object, add it to the ModelObjects list
				if (attr.Mode != ObjectMode.InstanceDefinitionObject)
					ModelObjects.Add (obj);
			}
		}

		/// <summary>
		/// Adds a Mesh to the ModelObjects List
		/// </summary>
		protected virtual void AddModelMesh (Mesh mesh, ObjectAttributes attr)
		{
			//Add this to the list of all the meshes
			if (AllMeshes == null)
				AllMeshes = new RhinoList<Mesh> ();

			AllMeshes.Add (mesh);

			Material material = new Material ();

			int materialIndex = -1;

			switch (attr.MaterialSource) {
		
			case (ObjectMaterialSource.MaterialFromLayer):
				if (attr.LayerIndex >= 0 && attr.LayerIndex < ModelFile.Layers.Count)
					materialIndex = ModelFile.Layers [attr.LayerIndex].RenderMaterialIndex;
				break;
			
			case (ObjectMaterialSource.MaterialFromObject):
				materialIndex = attr.MaterialIndex;
				break;

			case (ObjectMaterialSource.MaterialFromParent):
				materialIndex = attr.MaterialIndex;
				break;
			}

			if (materialIndex < 0 || materialIndex >= ModelFile.Materials.Count) {
				materialIndex = -1;
				material.Default ();
			} else {
				material = ModelFile.Materials [materialIndex];
			}

			object[] displayMeshes = DisplayMesh.CreateWithMesh (mesh, attr, material);

			ModelMesh modelMesh = new ModelMesh (displayMeshes, attr.ObjectId);

			AddModelObject (modelMesh, attr);
		}

		/// <summary>
		/// NOTE: Not called in currrent implementation.
		/// Models with large meshes must partition the meshes before displaying them on the device.
		/// Partitioning meshes is a lengthy process and can take > 80% of the model loading time.
		/// We save the raw VBO data of a mesh in an archive after a mesh has been partitioned
		/// and use that archive to create the VBOs next time we display the model.
		/// </summary>
		protected virtual bool LoadMeshCaches(Mesh mesh, ObjectAttributes attr, Material material)
		{
			string meshGUIDString = attr.ObjectId.ToString ();
			string meshCachePath = SupportPathForName (meshGUIDString + ".meshes");
			List<DisplayObject> displayMeshes = new List<DisplayObject> ();

			try {
				using (var meshFile = File.Open (meshCachePath, FileMode.Open)) {
					BinaryFormatter bin = new BinaryFormatter ();
					displayMeshes = (List<DisplayObject>)bin.Deserialize (meshFile);
				}
			} catch (IOException ex) {
				System.Diagnostics.Debug.WriteLine ("Could not Deserialize the DisplayMeshes with exception: {0}", ex.Message);
				Rhino.Runtime.HostUtils.ExceptionReport (ex);
			}

			if (displayMeshes == null)
				return false;

			foreach (DisplayMesh me in displayMeshes)
				me.RestoreUsingMesh (mesh, material);

			if (material.Transparency == 0)
				DisplayObjects.AddRange (displayMeshes);
			else
				TransparentObjects.AddRange (displayMeshes);

			return true;
		}

		/// <summary>
		/// Writes the displayMeshes to disk.
		/// </summary>
		public virtual void SaveDisplayMeshes (List<DisplayMesh> meshesToSave, Mesh mesh, ObjectAttributes attr, Material material) 
		{
			string meshGUIDString = attr.ObjectId.ToString ();
			string meshCachePath = SupportPathForName (meshGUIDString + ".meshes");

			try {
				using (var meshFile = File.Open (meshCachePath, FileMode.Create)) {
					BinaryFormatter bin = new BinaryFormatter();
					bin.Serialize(meshFile, meshesToSave);
				}
			} catch (IOException ex) {
				Rhino.Runtime.HostUtils.ExceptionReport (ex);
				System.Diagnostics.Debug.WriteLine ("Could not Serialize the DisplayMeshes with exception: {0}", ex.Message);
			}
		}
		#endregion

		#region Mesh Prep Events Progress and Exceptions
		/// <summary>
		/// Triggers a MeshPrep event and passes a measure of progress to any subscribers
		/// </summary>
		public virtual void MeshPreparationProgressEvent (float progress) 
		{
			if (m_meshPrepProgress == null)
				m_meshPrepProgress = new MeshPreparationProgress ();

			if (MeshPrep != null) {
				m_meshPrepProgress.MeshProgress = progress;
				m_meshPrepProgress.PreparationDidSucceed = false;
				MeshPrep (this, m_meshPrepProgress);
			}
		}

		/// <summary>
		/// Triggers a MeshPrep event and tells any subscribers that mesh prep has succeeded
		/// </summary>
		public virtual void MeshPreparationDidSucceed ()
		{ 
			if (m_meshPrepProgress == null)
				m_meshPrepProgress = new MeshPreparationProgress ();

			if (MeshPrep != null) {
				m_meshPrepProgress.PreparationDidSucceed = true;
				MeshPrep (this, m_meshPrepProgress);
			}
		}

		/// <summary>
		/// Triggers a MeshPrep event and tells any subscribers that mesh prep as failed
		/// </summary>
		public virtual void MeshPreparationDidFailWithException (Exception exception)
		{
			if (MeshPrep != null) {
				m_meshPrepProgress.PreparationDidSucceed = false;
				m_meshPrepProgress.FailException = exception;
				MeshPrep (this, m_meshPrepProgress);
			}

			InitializationFailed = true;
		}

		/// <summary>
		/// Platform-specific error handling for MeshExceptions is handled by this method
		/// </summary>
		public virtual Exception MeshException (string errorString)
		{
			Exception meshException = new Exception (errorString);
			return meshException;
		}
		#endregion

		#region Load Model From Bundle
		#if __IOS__
		/// <summary>
		/// Moves any model included in the default sample models from the Application bundle into the Documents folder.
		/// </summary>
		public virtual void LoadFromBundle ()
		{
			if (BundleName != null) {
				DocumentsFilename = Title + ".3dm";

				string sampleModelFilePath = Path.Combine ("Models", Title);

				// Copy the sample file from the App bundle to the Documents folder at ModelPath
				string fromPath = NSBundle.MainBundle.PathForResource(sampleModelFilePath, "3dm");
				string toPath = ModelPath;
				if (!(File.Exists(fromPath)))  
					System.Diagnostics.Debug.WriteLine("WARNING: Could not find the file: {0}", DocumentsFilename);

				if ((File.Exists (fromPath)) && (!File.Exists (toPath))) {
					try {
						File.Copy (fromPath, toPath, true);
					} catch (Exception ex) {
						System.Diagnostics.Debug.WriteLine (ex.Message);
						Rhino.Runtime.HostUtils.ExceptionReport (ex);
					}
				}

				// Since this file is created at initial launch, make sure that the no archive attribute
				// is set for this model in Documents.  The iRhino 3D app was rejected for creating files
				// that needed to be backed up when first launched.
				NSUrl modelPathURL = new NSUrl(ModelPath, false);
				ExcludeFromBackup(modelPathURL);

				Downloaded = true;
			}
		}
		#endif

		#if __ANDROID__
		/// <summary>
		/// Moves any model included in the default sample models from the Application bundle into the Documents folder.
		/// </summary>
		public virtual void LoadFromBundle(Android.Content.Context context)
		{
			if (BundleName != null) {
				DocumentsFilename = Title + ".3dm";
				string sampleModelFilePath = Path.Combine ("Models", DocumentsFilename);
				System.IO.Stream sampleModelStream;

				try {
					sampleModelStream = context.Assets.Open (sampleModelFilePath);
				} catch (Java.IO.FileNotFoundException ex) {
					System.Diagnostics.Debug.WriteLine ("WARNING: Could not find the file: {0}", ex.Message);
					Rhino.Runtime.HostUtils.ExceptionReport (ex);
					return;
				} 

				string toPath = ModelPath;

				if (!File.Exists(toPath)) {
					System.IO.FileStream outputModelStream = new System.IO.FileStream (ModelPath, FileMode.OpenOrCreate);

					byte[] buffer = new byte[8 * 1024];
					int len;
					while ( (len = sampleModelStream.Read(buffer, 0, buffer.Length)) > 0)
					{
						outputModelStream.Write(buffer, 0, len);
					}    

					outputModelStream.Flush ();
					outputModelStream.Close ();

					sampleModelStream.Close ();
				}

				Downloaded = true;
			}
		}
		#endif
		#endregion

		#region Utilities
		/// <summary>
		/// If documentsFilename is defined, return the full path to the filename.
		/// Create unique file name for model, save in documentsFilename, return full path.  
		/// </summary>
		public virtual string CreateModelPath() 
		{		
			var documentsPath = System.Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
			if (DocumentsFilename != string.Empty)
				return Path.Combine(documentsPath, DocumentsFilename);

			// Each model is stored in the ~/Documents folder (on iOS) or the ~/files/ folder (on Android), 
			// and since the user can access this folder through iTunes File Sharing, there may already be a file
			// with our URL name in this folder, so check for that an increment the filename in the folder if that
			if (BaseName != string.Empty) {
				string candidateName = BaseName + ".3dm";
				int index = 2;

				while (true) {
					string candidatePath = Path.Combine (documentsPath, candidateName);
					if (!(File.Exists (candidatePath))) {
						DocumentsFilename = candidateName;
						return candidatePath;
					}

					candidateName = BaseName + index.ToString () + ".3dm";
					index++;
				}
			} else 
				return string.Empty;
		}

		/// <summary>
		/// Returns the Model Object with the GUID provided
		/// </summary>
		public virtual ModelObject ModelObjectWithGUID(Guid guid)
		{
			ModelObject objectToRetreive;
			ModelObjectsDictionary.TryGetValue (guid, out objectToRetreive);
			return objectToRetreive;
		}

		/// <summary>
		/// Checks to see if the name of the file provided is equal to the DocumentsFilename in this RMModel
		/// </summary>
		public virtual bool HasDocumentsName (string documentsName) 
		{
			if (!String.IsNullOrEmpty(documentsName) && !String.IsNullOrEmpty(DocumentsFilename))
				return string.Equals (documentsName, DocumentsFilename, StringComparison.CurrentCultureIgnoreCase);
			return false;	
		}

		/// <summary>
		/// Gets size of the 3dm file - Model - (in bytes). Returns 0 if the file is null or cannot be found.
		/// </summary>
		public long GetFileSize () { 
			if (!string.IsNullOrEmpty(ModelPath)) {
				if (File.Exists (ModelPath)) {
					FileInfo fileInfo = new FileInfo(ModelPath);
					return fileInfo.Length;
				}
			} 
			return 0;
		}

		#if __IOS__
		/// <summary>
		/// iOS ONLY - To Comply with iOS iCloud backup restrictions
		/// <para>Makes sure that the no archive attribute is set for this url in Documents.</para>
		/// </summary>
		protected virtual void ExcludeFromBackup (NSUrl filePathURL)
		{
			filePathURL.SetResource(NSUrl.IsExcludedFromBackupKey, (NSNumber.FromBoolean(true)));
		}

		protected virtual void ShouldExcludeFromBackup (string filePath, bool exclude)
		{
			NSFileManager.SetSkipBackupAttribute (filePath, exclude); // backup will be skipped for this file
		}
		#endif

		/// <summary>
		/// <para>Android: Helper method for creating a full path from a file name that is in the 
		/// data/data/[AppName]/files/appsupport directory</para>
		/// <para>iOS: Helper method for creating a full path from a file name that is in the 
		/// ~/Library/Application Support directory</para>
		/// </summary>
		protected virtual string SupportPathFromDirectory(string directory, string filename)
		{
			string documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);

			#if __ANDROID__
			string appSupportPath = Path.Combine (documentsPath, "appsupport");
			#endif

			#if __IOS__
			string appSupportPath = Path.Combine (documentsPath, "..", "Library", "Application Support");
			#endif

			// ensure app support directory exists and, if it does not, create it...
			if (!System.IO.Directory.Exists (appSupportPath))
				System.IO.Directory.CreateDirectory(appSupportPath);

			// Make sure that the no archive attribute is set for this directory on iOS
			#if __IOS__
			NSUrl filePathURL = new NSUrl (appSupportPath, true);
			ExcludeFromBackup (filePathURL);
			#endif

			if (directory.Length > 0)
				appSupportPath = Path.Combine (appSupportPath, directory);
			if (filename.Length > 0)
				appSupportPath = Path.Combine (appSupportPath, filename);

			return appSupportPath;
		}

		/// <summary>
		/// <para>Android: Helper method for creating a full path from a file name that is in the 
		/// data/data/[AppName]/files/appsupport subdirectory</para>
		/// <para>iOS: Helper method for creating a full path for one of our files that is in the 
		/// ~/Library/Application Support subdirectory</para>
		/// </summary>
		public virtual string SupportPathForName(string fileOrDirectoryName) 
		{
			if (SupportDirectoryName == null) {
				string directoryName = string.Empty;
				string directoryPath = string.Empty;
			
				do {
					directoryName = System.Guid.NewGuid ().ToString ();
					directoryPath = SupportPathFromDirectory (directoryName, string.Empty);
				} while (System.IO.Directory.Exists(directoryPath));

				SupportDirectoryName = directoryName;
				System.IO.Directory.CreateDirectory(directoryPath);
			}

			return SupportPathFromDirectory (SupportDirectoryName, fileOrDirectoryName);
		}
		#endregion

		#region Cleanup
		/// <summary>
		/// Delete everything, including our containing directory
		/// </summary>
		public virtual void DeleteAll() 
		{
			DeleteCaches ();

			if (DocumentsFilename != string.Empty && File.Exists(ModelPath))
				System.IO.File.Delete(ModelPath);

			DocumentsFilename = string.Empty;
			Downloaded = false;
		}

		/// <summary>
		/// Revert to undownloaded status
		/// </summary>
		public virtual void UnDownload() 
		{
			DeleteAll ();
			Downloaded = false;
			Dispose ();
		}

		/// <summary>
		/// Delete all cached data but not the model
		/// </summary>
		public virtual void DeleteCaches() 
		{
			if (!string.IsNullOrEmpty(SupportDirectoryName)) {
				if (Directory.Exists (SupportPathForName (string.Empty))) { 
					try {
						Directory.Delete (SupportPathForName (string.Empty));
					} catch (Exception ex) {
						System.Diagnostics.Debug.WriteLine (ex.Message);
					}
				}
			}
				
			SupportDirectoryName = string.Empty;
		}
		#endregion
	}
}