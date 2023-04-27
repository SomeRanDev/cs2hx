using System.Reflection;
using System.Text.RegularExpressions;
using System.Net.Sockets;


namespace dll_reader {
	public static class Connection {
		public static Socket? socket = null;
		public static void WriteLine(string s) {
			Console.WriteLine("Sending: " + s);
			socket.Send(System.Text.Encoding.UTF8.GetBytes(s + "\n"));
		}
	}
}

class HxTypeParam {
	string Name;

	public HxTypeParam(Type t) {
		Name = t.Name;
	}

	public void Print() {
		dll_reader.Connection.WriteLine(Name);
	}
}

class HxField {
	public FieldInfo field;

	public HxField(FieldInfo f) {
		field = f;
	}

	public void Print() {
		dll_reader.Connection.WriteLine(field.Name);
		dll_reader.Connection.WriteLine(ReadDLL.FullName(field.FieldType));
	}
}

class HxProperty {
	PropertyInfo prop;

	public HxProperty(PropertyInfo p) {
		prop = p;
	}

	public void Print() {
		dll_reader.Connection.WriteLine(prop.Name);
		dll_reader.Connection.WriteLine(ReadDLL.FullName(prop.PropertyType));
		dll_reader.Connection.WriteLine(prop.CanRead.ToString());
		dll_reader.Connection.WriteLine(prop.CanWrite.ToString());
	}
}

class HxMethod {
	MethodInfo meth;

	public HxMethod(MethodInfo m) {
		meth = m;
	}

	public void Print() {
		dll_reader.Connection.WriteLine(meth.Name);
		dll_reader.Connection.WriteLine(ReadDLL.FullName(meth.ReturnType));
		dll_reader.Connection.WriteLine(meth.GetParameters().Length.ToString());
		foreach(var p in meth.GetParameters()) {
			dll_reader.Connection.WriteLine(p.Name);
			dll_reader.Connection.WriteLine(ReadDLL.FullName(p.ParameterType));
			dll_reader.Connection.WriteLine(p.DefaultValue != null ? "true" : "false");
		}
	}
}

class HxTypeDef {
	public List<HxTypeParam> Params = new List<HxTypeParam>();
	public string Namespace = "";
	public string Name = "";
	public bool IsInterface = false;
	public string SuperPath = "";
	public List<string> Interfaces = new List<string>();
	public List<HxField> Fields = new List<HxField>();
	public List<HxProperty> Props = new List<HxProperty>();
	public List<HxMethod> Methods = new List<HxMethod>();
	public string? Doc = null;

	public void Print() {
		dll_reader.Connection.WriteLine(ReadDLL.ConvertGenericTick(Name));
		dll_reader.Connection.WriteLine(IsInterface ? "true" : "false");
		dll_reader.Connection.WriteLine("cs" + (Namespace.Length > 0 ? "." : "") + Namespace.ToLower());
		dll_reader.Connection.WriteLine(SuperPath);
		dll_reader.Connection.WriteLine(Interfaces.Count.ToString());
		foreach(var i in Interfaces) {
			dll_reader.Connection.WriteLine(i);
		}

		dll_reader.Connection.WriteLine(Params.Count.ToString());
		foreach(var p in Params) {
			p.Print();
		}

		dll_reader.Connection.WriteLine(Fields.Count.ToString());
		foreach(var f in Fields) {
			f.Print();
		}

		dll_reader.Connection.WriteLine(Props.Count.ToString());
		foreach(var p in Props) {
			p.Print();
		}

		dll_reader.Connection.WriteLine(Methods.Count.ToString());
		foreach(var m in Methods) {
			m.Print();
		}
	}
}

class ReadDLL {
	public static string FullName(Type? t) {
		if(t == null)
			return "";

		if(t.IsGenericParameter || t.IsGenericMethodParameter || t.IsGenericTypeParameter) {
			return t.Name;
		}

		if(t.IsArray) {
			return "Array<" + FullName(t.GetElementType()) + ">";
		}

		switch(t.Namespace + "." + t.Name) {
			case "System.Void": return "Void";
			case "System.Int32": return "Int";
			case "System.Double": return "Float";
			case "System.Boolean": return "Bool";
		}

		var args = t.GetGenericArguments();
		var generics = new List<string>();
		foreach(var arg in args) {
			generics.Add(FullName(arg));
		}

		var tp = (args.Length > 0 ? ("<" + string.Join(", ", generics) + ">") : "");
		return "cs." + (t.Namespace != null ? (t.Namespace.ToLower() + ".") : "") + ConvertGenericTick(t.Name) + tp;
	}

	// Convert TYPENAME`123 -> TYPENAME_123
	public static string ConvertGenericTick(string Name) {
		Regex rx = new Regex(@"^(.*)`(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		var m = rx.Matches(Name);
		if(m.Count > 0) {
			return m[0].Groups[1].Value + "_" + m[0].Groups[2].Value;
		}
		return Name;
	}

	// Store references to all the assemblies that are loaded.
	static List<Assembly> Assemblies = new List<Assembly>();

	// Given a namespace and name, find a type.
	static Type? FindCsType(string HaxeNS, string HaxeName) {
		HaxeNS = HaxeNS.ToLower();
		var assemblies = AppDomain.CurrentDomain.GetAssemblies().Concat(Assemblies);
		foreach(var assembly in assemblies) {
			foreach(Type type in assembly.GetTypes()) {
				if((type.Namespace?.ToLower() ?? "") == HaxeNS && ConvertGenericTick(type.Name) == HaxeName) {
					return type;
				}
			}
		}
		return null;
	}

	// Note to future self:
	// - We need proper capitalization (OK: System.String vs WRONG: cs.system.String)
	// - Use "TypeName`X" to denote a type with X type arguments (OK: List`1 vs WRONG: List WRONG: List<T>)
	public static void Main(String[] args) {
		// Make sure we have enough arguments!!
		if(args.Length <= 0) {
			Console.WriteLine("<no type path provided>");
			return;
		}

		var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		// the first argument is the port number!
		var port = Int32.Parse(args[0]);
		Console.WriteLine("Connecting to localhost:" + port);
		socket.Connect("localhost", port);
		Console.WriteLine("Connected!");
		dll_reader.Connection.socket = socket;


		// The second argument is the type path we're looking for!
		var HaxePath = args[1];

		// Load all the assmblies provided!
		// dll_reader <type_path> <dll_1> <dll_2> ...
		for(int i = 2; i < args.Length; i++) {
			Assemblies.Add(Assembly.LoadFrom(args[i]));
		}

		// Separate the namespace part and the class part.
		// The Haxe namespaces must all be lowercase, so they are compared differently.
		var HaxePathMems = HaxePath.Split(".");
		var HaxeNS = string.Join(".", HaxePathMems[..^1]).ToLower();
		var HaxeName = HaxePathMems.Last();

		// Find the type
		var t = FindCsType(HaxeNS, HaxeName);
		if(t == null) {
			dll_reader.Connection.WriteLine("<no type found>");
			return;
		}

		// Retrieve all the type information and store in HxTypeDef
		var def = new HxTypeDef();

		def.Namespace = t.Namespace ?? "";
		def.Name = t.Name;
		def.IsInterface = t.IsInterface;
		def.SuperPath = FullName(t.BaseType);
		foreach(var i in t.GetInterfaces()) {
			def.Interfaces.Add(FullName(i));
		}
		foreach(var arg in t.GetGenericArguments()) {
			def.Params.Add(new HxTypeParam(arg));
		}

		foreach(var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
			def.Fields.Add(new HxField(f));
		}
		foreach(var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
			def.Props.Add(new HxProperty(p));
		}
		foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
			def.Methods.Add(new HxMethod(m));
		}

		// Print
		def.Print();
		// end
		dll_reader.Connection.WriteLine("__exit__");
		socket.Shutdown(SocketShutdown.Both);
    	socket.Close();
		Console.WriteLine("Done!");
	}
}
