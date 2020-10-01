Aspectimum is an aspect-oriented programming tool that implements source-level weaving of advices 
for C# code using combination of Roslyn and T4 template technologies.
 
Demo folder contains three projects:

1. Demo/AspectimumDemo is a main execution project that includes reference to Demo/Aop/Aspectimum.Demo.Lib library project

2. Demo/Aop/Aspectimum.Demo.Lib is a project that is automatically created by the Aspectimum tool and 
   should not be modified directly by a developer (because any changes that are done to the project,
   will be orverwritten the next time weaving is done).

3. Demo/Libraries/Aspectimum.Demo.Lib/ is a project that contains original source code developed by a programmer.
   After a programmer completes changes to the project, validates that no error during compilation of the project, 
   he/she can run the Aspectimum    to weave advices to the destination project (Demo/Aop/Aspectimum.Demo.Lib) 
   on source code level.
   
   See workflow diagram at https://raw.githubusercontent.com/eugenie32b/aspectimum/master/Demo/Doc/demo_project_dev_flow.png
   
Templates folder contains definitions of advices as templates in T4 format, that would produce source code 
that would be weaved into original source code of a program.
 
Tools folder contains two projects:

1. Aop.Common is a library that includes just one class AopTemplate that is an attribute that would allow you 
   to specify pointcuts in source level of a program.

2. AopBuilder is a project for a tool that do weaving of advices produced from templates into a destination project.
   

Useful resources to help developement of templates using T4 and Roslyn technologies:

1. To simplify Roslyn navigation you can walk through syntax tree using a online visualizer 
   https://sharplab.io/

2. Convert c# source code shows syntax tree API calls to construct its syntax tree

   https://roslynquoter.azurewebsites.net/

3. T4 Toolbox

	http://olegsych.com/T4Toolbox/

4. Syntax Visualizer 

   https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/syntax-visualizer?tabs=csharp



Example of how to run the tool from command line (from Windows).
Prerequisite: you must have a .Net Core 3.1 or later installed on your computer.

1. Change your current folder to a root folder of the project

2. Build the tool in Debug mode, from command line

dotnet build

3. Execute commands

cd Tools\AopBuilder\bin\Debug\netcoreapp3.1

AopBuilder.exe -s=..\..\..\..\..\Demo\Libraries\Aspectimum.Demo.Lib\ -d=..\..\..\..\..\Demo\Aop\Aspectimum.Demo.Lib\ -t ..\..\..\..\..\Templates\ -f *.cs 



If everything goes ok, you will see something like:

Class:  Person
        Processing template NotifyPropertyChangedClass
Class:  Customer
Class:  ConsoleDemo
        Processing template StaticAnalyzer
Method:  SecondDemo
        Processing template CatchExceptionMethod
Property:  FullName
        Processing template NotifyPropertyChanged
Property:  FullName
        Processing template CacheProperty
Property:  FirstName
        Processing template NotifyPropertyChanged
Property:  LastName
        Processing template NotifyPropertyChanged
Property:  Age
        Processing template NotifyPropertyChanged
Property:  CreditScore
        Processing template NotifyPropertyChanged
        Processing template SecondDemoUsing
Class:  Person
Class:  Customer
Class:  ConsoleDemo
        Processing template ResourceReplacer
Class:  Demo
Class:  Demo


NOTE: Because a file Demo\Aop\Aspectimum.Demo.Lib\ConsoleDemo.cs will be regenerated and some errors will be included there on purpose.
The next time, if you try to build the solution, the build will fail for DEMO project. It is expected behavior.

AopBuilder.exe will be successfully rebuilt and could be used without any problems.

If you perfer to use a demo class without any errors, please replace a file Demo\Libraries\Aspectimum.Demo.Lib\ConsoleDemo.cs
with error-free version from Demo\NoErrorConsoleDemo\ConsoleDemo.cs 

--------------------------------------------------------------------------------------------------------------------------
WARNING: If you work in VisualStudio then "Templates" you see, is a _solution_ folder, not a file system one.
If you want to add a new template, make sure you create t4 file in the "Templates" folder on file system and 
add the template to "Templates" folder in VisualStudio as an existing item. 

Otherwise the builder tool will not be able to find the template.
