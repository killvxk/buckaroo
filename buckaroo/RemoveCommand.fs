module Buckaroo.RemoveCommand

open System
open Buckaroo.RichOutput

let task (context : Tasks.TaskContext) (packages : List<PackageIdentifier>) = async {
  context.Console.Write(
    (text "Removing [ ") + 
    (packages |> Seq.map PackageIdentifier.showRich |> RichOutput.concat (text " ")) + 
    " ]... "
  )

  let! manifest = Tasks.readManifest "."

  let newManifest =
    packages
    |> Seq.fold (fun state next -> Manifest.remove state next) manifest
  
  if manifest = newManifest 
  then
    context.Console.Write("No changes to be made. " |> text |> foreground ConsoleColor.Green)
  else
    let! maybeLock = Tasks.readLockIfPresent
    let! resolution = Solver.solve context newManifest ResolutionStyle.Quick maybeLock 

    match resolution with
    | Ok solution -> 
      let newLock = Lock.fromManifestAndSolution newManifest solution
      
      let lock = 
        maybeLock 
        |> Option.defaultValue { newLock with Packages = Map.empty }
      
      context.Console.Write(Lock.showDiff lock newLock)

      let removedPackages = 
        lock.Packages
        |> Map.toSeq
        |> Seq.filter (fun (package, _) -> newLock.Packages |> Map.containsKey package |> not)

      for (package, _) in removedPackages do 
        let path = InstallCommand.packageInstallPath [] package
        context.Console.Write("Deleting " + path + "... ")
        Files.deleteDirectoryIfExists path |> ignore

      do! Tasks.writeManifest newManifest
      do! Tasks.writeLock newLock
      do! InstallCommand.task context

      context.Console.Write("Done. " |> text |> foreground ConsoleColor.Green)

    | x -> 
      context.Console.Write(string x)
      context.Console.Write("No changes were written. " |> text |> foreground ConsoleColor.Green)
}
