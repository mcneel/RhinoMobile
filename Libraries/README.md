Notes on RhinoMobile libraries
==============================
This project depends on RhinoCommon to make calls into openNURBS with p/invoke.  The linked rhinocommon folder in 
Libraries points to the publically available version of RhinoCommon: https://github.com/mcneel/rhinocommon
which, in turn, depends on the publically available openNURBS: http://www.opennurbs.org.

To get RhinoMobile (RhinoMobile.Droid, RhinoMobile.Touch) to build against rhinocommon, 
rhinocommon must be placed in a folder parallel to RVCrossPlatform.  For example:

<pre><code>SomeFolder
    |
    |______ rhinocommon
    |
    |______ YourProject
</code></pre>

If the folder structure looks like the above, it should work.  Alternatively, you can modify the projects to point
to a location of your choice by editing the vcproj files.

openNURBS - The copy of opennurbs used is in the rhinocommon/c/opennurbs folder.

A build script for Mac OS X (build_mobile.sh) has been included in the rhinocommon/c folder.

_QUICK OPENNURBS REBUILD (Both platforms):_
Run the build_mobile.sh (in Terminal, prefix it with a ./) to rebuild openNURBS for the target platforms.

iOS Build Instructions
--------------------
openNURBS compiles to a static library (universal fat binary) that will run on armv7, armv7s and i386 architectures. 
The libopennurbs.a static library here is compiled with XCode command-line tools following these directions: 
http://docs.xamarin.com/guides/ios/advanced_topics/native_interop

libopennurbs.linkwith.cs adds a LinkWith attribute to the assembly that forces the compiler to "link with" 
libopennurbs.a.  NOTE: Any static library added and linked to the project that wishes to use the LinkWith attribute
MUST have the Build Action set to EmbeddedResource and the Resource ID set to the exact name of the library...so, for
example, libopennurbs.a's Build Action would be set to EmbeddedResource and the Resource ID to libopennurbs.a (not
namespace.namespace.libopennurbs.a - which is the default when you switch the Build Action).

As an alternative to using the LinkWith attribute, extra mtouch arguments to add to the RV.Touch project are:
<pre><code>-cxx -gcc_flags "-L${SolutionDir}/HelloRhino.Touch/libs/ -lopennurbs -force_load ${SolutionDir}/HelloRhino.Touch/libs/libopennurbs.a" </code></pre>

Android Build Instructions
--------------------
openNURBS compiles to a shared objects library on Android that will run within the JNI (Java Native Interface) on 
armv7-A, armv5TE and i386 architectures.  This shared objects library (libopennurbs.so) is built with the Android NDK.  
In order to build the ON library for Android, you must have the Android NDK installed.  Typically, Xamarin ships the 
NDK with Xamarin.Android and it can be found in:

/Users/you/Library/Developer/Xamarin/android-ndk/android-ndk-r??

	Note: to add the Android NDK to your terminal path, add the following lines to your .bash-profile:
		export ANDROID_SDK="/Users/<you>/Library/Developer/Xamarin/android-sdk-mac_x86/"
		export ANDROID_NDK="/Users/<you>/Library/Developer/Xamarin/android-ndk/android-ndk-r??/"
		export PATH="$PATH:$ANDROID_SDK/tools:$ANDROID_SDK/platform-tools:$ANDROID_NDK"

The key utility is called ndk-build which runs the arm-eabi-gcc compiler.  ndk-build MUST be run from this path:
rhinocommon/c/.  This folder contains the jni folder which, in turn, uses the Application.mk and Android.mk makefiles as inputs to
the NDK compiler.  The resulting .so libraries are stored in the build/Release-android/libs/ folder, which must be added to your
project with the following settings:
Build action: AndroidNativeLibrary | Copy to output directory: Always Copy

To rebuild the libopennurbs.so libraries, in terminal, navigate to the rhinocommon/c/ folder and simply run the build script
with the android argument.  Move the resulting libraries into your project.

NOTE: The current Application.mk builds for three different targets: armv7-A, armv5TE and x86.  If you want to build
for only a single target (to speed up testing and changes, for example) you will need to modify the Application.mk file
in the rhinocommon/c/jni/ folder.  The build targets are set on line 9:

<pre><code>APP_ABI := armeabi armeabi-v7a x86
To change this to build for only x86, just change the line to:
APP_ABI := x86 #armeabi armeabi-v7a </code></pre>

(The Application.mk abd Android.mk are sensitive to line endings and invisible characters, so be careful editing.)

WindowsPhone Build Instructions
--------------------
Forthcoming
