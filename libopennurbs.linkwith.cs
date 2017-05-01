using System;

#if __MONOTOUCH__
using MonoTouch.ObjCRuntime;
[assembly: LinkWith ("libopennurbs.a", LinkTarget.Simulator | LinkTarget.ArmV7 | LinkTarget.ArmV7s, ForceLoad = true, IsCxx = true)]
#endif

#if __IOS__
using ObjCRuntime;
[assembly: LinkWith ("libopennurbs.a", LinkTarget.Simulator | LinkTarget.Arm64, ForceLoad = true, IsCxx = true)]
#endif
 