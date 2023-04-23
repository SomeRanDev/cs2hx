import haxe.io.Path;
import sys.io.File;

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
	final srcPath = "./bin/Release/net7.0";
	final outPath = "../dll_reader_bin/" + Sys.systemName();

	if(sys.FileSystem.exists(srcPath)) {
		if(!sys.FileSystem.exists(outPath)) {
			sys.FileSystem.createDirectory(outPath);
		}
		copyFolder(srcPath, outPath);
	} else {
		Sys.println("OnBuildSucess.hx could not find the executable to copy.");
	}
}

function copyFolder(dir: String, out: String) {
	for(f in sys.FileSystem.readDirectory(dir)) {
		File.copy(Path.join([dir, f]), Path.join([out, f]));
	}
}
