import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class Ca65TaskProvider implements vscode.TaskProvider {
    static readonly Type = 'ca65';
    
    private tasks: vscode.Task[] | undefined;

    constructor() {}

    public async provideTasks(): Promise<vscode.Task[]> {
        return this.getTasks();
    }

    public resolveTask(task: vscode.Task): vscode.Task | undefined {
        // For now, just return the task as-is
        return task;
    }

    private async getTasks(): Promise<vscode.Task[]> {
        if (this.tasks !== undefined) {
            return this.tasks;
        }

        this.tasks = [];

        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders) {
            return this.tasks;
        }

        // Create a single "build current file" task for each workspace folder
        for (const folder of workspaceFolders) {
            const task = this.createBuildCurrentFileTask(folder);
            if (task) {
                this.tasks.push(task);
            }
        }

        return this.tasks;
    }

    private createBuildCurrentFileTask(workspaceFolder: vscode.WorkspaceFolder): vscode.Task | undefined {
        // Task definition
        const taskDefinition: vscode.TaskDefinition = {
            type: Ca65TaskProvider.Type,
            task: 'build-current-file'
        };

        // Use VS Code variables for current file
        // ${fileBasename} = filename with extension (e.g., program.asm)
        // ${fileBasenameNoExtension} = filename without extension (e.g., program)
        // ${fileDirname} = directory of current file
        // 
        // Uses c64-asm.cfg as it's a reasonable default for assembly programs.
        // Without a config, cl65 uses c64.cfg which expects C runtime segments.
        // For custom configs, users should create their own task in tasks.json
        
        // Build command with echo to produce output (helps VS Code detect completion)
        const command = `cl65 -g \${fileBasename} -o \${fileBasenameNoExtension}.prg -C c64-asm.cfg -Wl -Ln,\${fileBasenameNoExtension}.lbl -Wl --dbgfile,\${fileBasenameNoExtension}.dbg -Wl -m,\${fileBasenameNoExtension}.map && echo "Build complete"`;

        const execution = new vscode.ShellExecution(command, {
            cwd: '${fileDirname}'
        });

        // Create task without problem matcher - will complete when shell command exits
        const task = new vscode.Task(
            taskDefinition,
            workspaceFolder,
            'build current file (C64)',
            Ca65TaskProvider.Type,
            execution
        );

        // Explicitly mark as non-background task
        task.isBackground = false;
        
        // Set task properties
        task.group = vscode.TaskGroup.Build;
        task.presentationOptions = {
            reveal: vscode.TaskRevealKind.Always,
            echo: true,
            focus: false,
            panel: vscode.TaskPanelKind.Shared,
            showReuseMessage: false,
            clear: false
        };

        return task;
    }

    public clear() {
        this.tasks = undefined;
    }
}
