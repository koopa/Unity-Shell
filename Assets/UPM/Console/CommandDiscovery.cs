﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class CommandDiscovery
{
    private StringToEnumConverter enumConverter = new StringToEnumConverter();
    private List<ConversionMapping> conversions = new List<ConversionMapping>()
    {
    };
    private Dictionary<string, Command> commandMap;

    public static CommandDiscovery Build(params Assembly[] _assemblies)
    {
        // add Project Assemblies
        List<Assembly> assemblies = _assemblies.ToList(); // sorry for converting to list, but it makes it easier to add the ProjectAssemblies, maybe we can make it so we dont need to add anyassemblies but it gets all the assemblies on Build
        assemblies.AddRange(GetProjectAssemblies()); // now we might get duplicates, but because we only add commands that are diffrent I think its okey

        var commands = new Dictionary<string, Command>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                                       BindingFlags.Public | BindingFlags.NonPublic |
                                                       BindingFlags.Default))
                {
                    foreach (var attribute in method.GetCustomAttributes(false).OfType<CommandAttribute>())
                    {
                        var command = new Command(method, string.IsNullOrEmpty(attribute.CustomName) ? method.Name : attribute.CustomName); // added so we can have a custom Name :)

                        if (commands.ContainsKey(command.ProgramName))
                        {
                            Debug.LogWarning("A command with the given name already exists: " + command.ProgramName);
                        }
                        else
                        {
                            commands.Add(command.ProgramName, command);
                        }
                    }
                }
            }
        }

        return new CommandDiscovery(commands);
    }

    protected CommandDiscovery(Dictionary<string, Command> commands)
    {
        commandMap = commands;
    }

    /// <summary>
    /// gets all Assemblies inside this Unity Project, so we can have commands outside of the "General.cs" script
    /// </summary>
    /// <returns></returns>
    public static Assembly[] GetProjectAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Where((Assembly assembly) => assembly.FullName.Contains("Assembly")).ToArray();
    }

    public void Invoke(string methodName)
    {
        var splitString = methodName.Split(' ');
        if (commandMap.ContainsKey(splitString[0]))
        {
            var argumentList = GenerateArgumentList(splitString.Skip(1));

            Execute(commandMap[splitString[0]].Program, argumentList);
        }
        else
        {
            Debug.Log("Cant find Command: " + splitString[0]);
        }
    }

    private IEnumerable<string> GenerateArgumentList(IEnumerable<string> arguments)
    {
        return arguments;
    }

    private void Execute(MethodInfo program, IEnumerable<string> arguments, Type activator = null)
    {
        if (program.IsStatic)
        {
            Execute(null, program, arguments);
        }
        else
        {
            //TODO fix non Static, because then we need to execute with the class that has this Methode/Program
            object instance = Activator.CreateInstance(activator); // activator = Type = the class/Type that has the Methode ?
            Execute(instance, program, arguments);
        }
    }

    private void Execute(object source, MethodInfo program, IEnumerable<string> arguments)
    {
        List<object> args = new List<object>();
        int index = 0;
        var parameters = program.GetParameters();
        var stringArgs = arguments.ToArray();
        foreach (var argument in stringArgs)
        {
            if (parameters[index].ParameterType == argument.GetType())
            {
                args.Add(argument);
            }
            else
            {
                args.Add(ConvertTo(argument, parameters[index].ParameterType));
            }
            index++;
        }
        program.Invoke(source, args.ToArray());
    }

    private object ConvertTo(string argument, Type parameterType)
    {
        foreach (var converter in conversions)
        {
            if (converter.SourceType == argument.GetType())
            {
                if (converter.TargetType == parameterType)
                {
                    Debug.Log("Converting with " + converter.Converter.ToString());
                    return converter.Converter.GeneralConversion(argument);
                }
            }
        }
        // Didn't find any available converters
        if (parameterType.IsSubclassOf(typeof(Enum)))
        {
            Debug.Log("Converting with automatic Enum Converter");
            enumConverter.ConversionEnumType = parameterType;
            return enumConverter.Convert(argument);
        }

        return null; //TODO: Fix this
    }
}