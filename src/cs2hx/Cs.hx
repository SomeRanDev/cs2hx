package cs2hx;

import sys.net.Socket;
import haxe.io.Path;
import sys.io.Process;

import haxe.macro.Context;
import haxe.macro.Expr;

/**
	Main module used to configure and use this project.
**/
class Cs {
	@:noCompletion static var _setupTypeNotFound: Bool = false;

	/**
		Stores a list of all the C# .dlls added using `addDll`.
	**/
	static var autoDlls: Array<String> = [];

	static var randomPort = 6114 + Std.random(200);

	/**
		Add a C# .dll to automatically add its types to the project at
		compile-time.
	**/
	public static function addDll(dllPath: String): Void {
		#if macro
		if(!sys.FileSystem.exists(dllPath)) {
			throw dllPath + " could not be found.";
		}
		autoDlls.push(dllPath);

		//Context.defineType(resolveCSType("cs.system.Console"));

		if(!_setupTypeNotFound) {
			Context.onTypeNotFound(resolveCSType);
			_setupTypeNotFound = true;
		}
		#else
		throw "Can only call during compile time.";
		#end
	}

	/**
		Resolve a C# type from one of the added C# .dlls from a
		Haxe type path.
	**/
	public static function resolveCSType(name: String): Null<TypeDefinition> {
		if(name == null || name.length == 0 || StringTools.endsWith(StringTools.trim(name), ".")) {
			return null;
		}
		if(StringTools.startsWith(name, "cs.")) {
			final parts = name.split(".");
			if (parts.remove("cs")) {
				// add port
				final cmd = "\"" + dllReaderExePath() + "\" " + randomPort + " " + parts.join(".");
				switch Sys.systemName() {
					case "Mac", "Linux", "BSD":
						Sys.command("chmod +x " + "\"" + dllReaderExePath() + "\"");
					default:
				}
				final server = new Socket();
				server.bind(new sys.net.Host("localhost"),randomPort);
				server.setBlocking(false);
				server.listen(1);
				// create client
				trace("create process");
				Sys.println(cmd);
				final process = new Process(cmd);
				// client socket
				var client:Socket = null;
				// lines
				final all:Array<String> = [];
				var line:String = "";
				while (true) {
					if (client == null) {
						trace("wait for accept");
						client = server.accept();
					}
					try {
						line = client.input.readLine();
						switch line {
							case "__exit__":
								client.close();
								break;
							default:
								all.push(line);
						}
						
					}catch(_) {
						Sys.sleep(0.1);
					}
				}
				// Get exit code.
				trace("waiting for exit code");
				server.close();
				final ec = process.exitCode();
				if(ec == 0) {
					final result = readDllReader(all, name);
					return result;
				} else {
					Sys.println("dll_reader exited with code " + ec + "\n\n" + process.stderr.readAll());
				}
			}

			final name = parts.pop();
			return {
				pos: makeNoPos(),
				params: [],
				pack: ["cs"].concat(parts),
				name: name,
				kind: TDClass(null, [], false, false, false),
				isExtern: true,
				fields: []
			}

		}
		return null;
	}

	static function dllReaderExePath(infos: Null<haxe.PosInfos> = null) {
		final p = new haxe.io.Path(infos.fileName);
		if(p.dir == null) throw "Could not find directory of Cs.hx";
		final parentDir = Path.normalize(Path.join([p.dir, "..", ".."]));
		return Path.join([parentDir, "dll_reader_bin", Sys.systemName(), "dll_reader"]);
	}

	static function makeNoPos(): Position {
		return #if macro Context.makePosition({min: 0, max: 0, file:"?"}) #else {min: 0, max: 0, file:"?"} #end;
	}

	static function readDllReader(input: Array<String>, name: String): Null<TypeDefinition> {
		// If a type isn't found, the output starts with <
		final firstLine = input[0];
		if(firstLine != null && StringTools.startsWith(firstLine, "<")) {
			#if debug_cs2hx
			Sys.println("Coult not resolve: " + name + "\n\n" + firstLine + "\n");
			#end
			return null;
		}

		// Simple functions for parsing the output.
		var count = 1;
		final next = () -> count < input.length ? input[count++] : "";
		final nextTypePath = (allowNull = false) -> {
			final path = next();
			if(!allowNull && path.length <= 0) {
				throw "Unexpected empty type path";
			}
			//if(path.length > 0) trace(stringToTypePath(path));
			return path.length > 0 ? stringToTypePath(path) : null;
		}

		// ---

		final nopos: Position = makeNoPos();

		// ---

		final name = firstLine;
		final isInterface = next() == "true";
		final namespaces = next().split(".");
		final superCls = nextTypePath(true);

		final interfaces: Array<TypePath> = [];
		final interfaceCount = Std.parseInt(next());
		for(i in 0...interfaceCount) {
			interfaces.push(nextTypePath());
		}

		final typeParamNames = [];
		final typeParamCount = Std.parseInt(next());
		for(i in 0...typeParamCount) {
			typeParamNames.push(next());
		}

		final fieldNameUsage: Map<String,Int> = [];

		function addField(arr: Array<Field>, index: Int,field: Field) {
			final name = field.name;
			final isOverload = if(fieldNameUsage.exists(name)) {
				final index = fieldNameUsage.get(name);
				if(index >= 0) {
					fieldNameUsage.set(name, -1);
					arr[index].access.push(AOverload);
				}
				true;
			} else {
				fieldNameUsage.set(name, index);
				false;
			}

			field.access = [APublic];
			if(isOverload) field.access.push(AOverload);

			arr.push(field);
		}

		final fields: Array<Field> = [];
		final fieldCount = Std.parseInt(next());
		for(i in 0...fieldCount) {
			final name = next();
			final tp = nextTypePath();
			addField(fields, i, {
				pos: nopos,
				name: name,
				kind: FVar(TPath(tp), null)
			});
		}

		final props: Array<Field> = [];
		final propCount = Std.parseInt(next());
		for(i in 0...propCount) {
			final name = next();
			final tp = nextTypePath();
			final canRead = next() == "true";
			final canWrite = next() == "true";
			if(canRead || canWrite) {
				addField(props, i, {
					pos: nopos,
					name: name,
					kind: FProp(canRead ? "default" : "never", canWrite ? "default" : "never", TPath(tp), null)
				});
			}
		}

		final funs: Array<Field> = [];
		final funCount = Std.parseInt(next());
		for(i in 0...funCount) {
			final name = next();
			final retTp = nextTypePath();

			final args: Array<FunctionArg> = [];
			final argCount = Std.parseInt(next());
			for(j in 0...argCount) {
				final argName = next();
				final tp = nextTypePath();
				final opt = next() == "true";
				args.push({
					name: argName,
					type: TPath(tp),
					opt: opt
				});
			}

			addField(funs, i, {
				pos: nopos,
				name: name,
				kind: FFun({
					ret: TPath(retTp),
					args: args
				})
			});
		}

		return {
			pos: nopos,
			params: typeParamNames.length > 0 ? typeParamNames.map(n -> ({ name: n } : TypeParamDecl)) : null,
			pack: namespaces.map(n -> n.toLowerCase()),
			name: name,
			kind: TDClass(superCls, interfaces, isInterface, false, false),
			isExtern: true,
			fields: fields.concat(props.concat(funs)),
			doc: "Generated using hx2cs"
		};
	}

	static function stringToTypePath(csTypeStr: String): haxe.macro.Expr.TypePath {
		if(csTypeStr.length <= 0) {
			throw "`stringToTypePath` provided empty String";
		}

		csTypeStr = StringTools.trim(csTypeStr);

		var name = csTypeStr;
		var params: Array<TypeParam> = [];
		if(StringTools.endsWith(csTypeStr, ">")) {
			final parts = ~/</.split(csTypeStr);
			// if(parts.length < 2) {
			// 	throw "Weird C# type string: " + csTypeStr;
			// }
			// parts[1] = parts[1].substr(0, parts[1].length - 1);
			// name = parts[0];
			// if(StringTools.contains(parts[1], ">")) {
			// 	throw "Test : " + parts[1];
			// }

			function parseTypes(t:Array<String>):Array<TypePath> {
				final result = [];
				var name = "";
				while (t.length > 0) {
					final c = t.shift();
					if (StringTools.isSpace(c, 0)) {}
					else if (c == ",") {
						result.push(_strToPath(name, []));
						name = "";
					} else if (c == ">") {
						result.push(_strToPath(name, []));
						return result;
					} else if (c == "<") {
						final pp:Array<TypePath> = parseTypes(t);
						result.push(_strToPath(name, pp.map(p -> TPType(TPath(p)))));
					} else {
						name += c;
					}
				}
		
				throw "Error parsing " + t.join("");
			}

			final result = _strToPath(parts[0], parseTypes(parts[1].split("")).map(p -> TPType(TPath(p))));
			trace(result);
			return result;

			//params = parts[1].split(",").map(t -> TPType(TPath(stringToTypePath(t))));

			// Comment this out for weird issue??
			// Too many type parameters?? How to fix this???
			//trace(csTypeStr);
			//if(name == "cs.system.ValueTuple" || name == "cs.system.IComparable" || name == "cs.system.Array") {
			//	return switch(macro : Dynamic) { case TPath(p): p; case _: throw "Impossible"; }
			//}
		}
		return _strToPath(name, params);
	}

	static function _strToPath(s: String, params: Array<TypeParam>): haxe.macro.Expr.TypePath {
		return switch(haxe.macro.MacroStringTools.toComplex(s)) {
			case TPath(p): {
				{
					name: p.name,
					pack: p.pack,
					params: params 
				}
			}
			case _: throw "Impossible";
		}
	}
}
