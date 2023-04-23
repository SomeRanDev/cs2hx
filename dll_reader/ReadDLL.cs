using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

class HxTypeParam {
	string Name;

	public HxTypeParam(Type t) {
		Name = t.Name;
	}

	public void Print() {
		Console.WriteLine(Name);
	}
}

class HxField {
	public FieldInfo field;

	public HxField(FieldInfo f) {
		field = f;
	}

	public void Print() {
		Console.WriteLine(field.Name);
		Console.WriteLine(ReadDLL.FullName(field.FieldType));
	}
}

class HxProperty {
	PropertyInfo prop;

	public HxProperty(PropertyInfo p) {
		prop = p;
	}

	public void Print() {
		Console.WriteLine(prop.Name);
		Console.WriteLine(ReadDLL.FullName(prop.PropertyType));
		Console.WriteLine(prop.CanRead);
		Console.WriteLine(prop.CanWrite);
	}
}

class HxMethod {
	MethodInfo meth;

	public HxMethod(MethodInfo m) {
		meth = m;
	}

	public void Print() {
		Console.WriteLine(meth.Name);
		Console.WriteLine(ReadDLL.FullName(meth.ReturnType));
		Console.WriteLine(meth.GetParameters().Length);
		foreach(var p in meth.GetParameters()) {
			Console.WriteLine(p.Name);
			Console.WriteLine(ReadDLL.FullName(p.ParameterType));
			Console.WriteLine(p.DefaultValue != null ? "true" : "false");
		}
	}
}

class HxTypeDef {
	public List<HxTypeParam> Params = new List<HxTypeParam>();
	public string Namespace = "";
	public string Name = "";
	public string SuperPath = "";
	public List<string> Interfaces = new List<string>();
	public List<HxField> Fields = new List<HxField>();
	public List<HxProperty> Props = new List<HxProperty>();
	public List<HxMethod> Methods = new List<HxMethod>();
	public string? Doc = null;

	public void Print() {
		Console.WriteLine(Name.Split("`")[0]);
		Console.WriteLine("cs" + (Namespace.Length > 0 ? "." : "") + Namespace.ToLower());
		Console.WriteLine(SuperPath);
		Console.WriteLine(Interfaces.Count);
		foreach(var i in Interfaces) {
			Console.WriteLine(i);
		}

		Console.WriteLine(Params.Count);
		foreach(var p in Params) {
			p.Print();
		}

		Console.WriteLine(Fields.Count);
		foreach(var f in Fields) {
			f.Print();
		}

		Console.WriteLine(Props.Count);
		foreach(var p in Props) {
			p.Print();
		}

		Console.WriteLine(Methods.Count);
		foreach(var m in Methods) {
			m.Print();
		}
	}
}

class ReadDLL {
	public static string FullName(Type? t) {
		if(t == null)
			return "";

		if(t.IsGenericParameter || t.IsGenericMethodParameter) {
			return t.Name;
		}

		if(t.IsArray) {
			return FullName(t.GetElementType()) + "[]";
		}

		switch(t.Namespace + "." + t.Name) {
			case "System.Void": return "Void";
			case "System.Int32": return "Int";
			case "System.Double": return "Float";
		}

		var args = t.GetGenericArguments();
		var generics = new List<string>();
		foreach(var arg in args) {
			generics.Add(FullName(arg));
		}

		var tp = (args.Length > 0 ? ("<" + string.Join(", ", generics) + ">") : "");
		return "cs." + (t.Namespace != null ? (t.Namespace.ToLower() + ".") : "") + t.Name.Split("`")[0] + tp;
	}

	// Note to future self:
	// - We need proper capitalization (OK: System.String vs WRONG: cs.system.String)
	// - Use "TypeName`X" to denote a type with X type arguments (OK: List`1 vs WRONG: List WRONG: List<T>)
	public static void Main(String[] args) {
		if(args.Length > 0) {
			var t = Type.GetType(args[0]);

			if(t == null) {
				Console.WriteLine("<no type found>");
				return;
			}

			var def = new HxTypeDef();

			def.Namespace = t.Namespace ?? "";
			def.Name = t.Name;
			def.SuperPath = FullName(t.BaseType);
			foreach(var i in t.GetInterfaces()) {
				def.Interfaces.Add(FullName(i));
			}
			foreach(var arg in t.GetGenericArguments()) {
				def.Params.Add(new HxTypeParam(arg));
			}

			var fields = t.GetFields(BindingFlags.Public);
			foreach(var f in fields) {
				def.Fields.Add(new HxField(f));
			}
			foreach(var p in t.GetProperties(BindingFlags.Public)) {
				def.Props.Add(new HxProperty(p));
			}
			foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
				def.Methods.Add(new HxMethod(m));
			}

			def.Print();
		}
	}
}
