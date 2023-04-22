/**
	This Haxe script is automatically called by Visual Studio
	upon a successful build of the dll_reader C# project.

	Copies the executable from where Visual Studio places it to
	the root of the cs2hx project.

	TODO: Untested on Mac/Linux.
**/
function main() {
	final ext = Sys.systemName() == "Windows" ? ".exe" : "";
	final filename = "dll_reader" + ext;
	final srcPath = "./bin/Release/net7.0/" + filename;
	if(sys.FileSystem.exists(srcPath)) {
		final destPath = "../" + filename;
		sys.io.File.copy(srcPath, destPath);
	} else {
		Sys.println("OnBuildSucess.hx could not find the executable to copy.");
	}
}