import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TaskItem } from '../../../core/models/task.model';
import { TaskStatusBadgeComponent } from '../task-status-badge/task-status-badge.component';

@Component({
  selector: 'app-task-card',
  standalone: true,
  imports: [DatePipe, TaskStatusBadgeComponent],
  template: `
    <div class="card bg-base-100 shadow-sm border border-base-300 hover:shadow-md transition-shadow min-h-[140px]">
      <div class="card-body p-4 flex flex-col justify-between">
        <div>
          <div class="flex items-start justify-between gap-2 mb-2">
            <h3 class="card-title text-base leading-tight">{{ task().title }}</h3>
            <app-task-status-badge [status]="task().status" />
          </div>
          <p class="text-sm text-base-content/70 line-clamp-2 min-h-[2.5rem]">
            {{ task().description }}
          </p>
        </div>

        <div class="flex items-center justify-between mt-2">
          <span class="text-xs text-base-content/50">
            Due: {{ task().dueDate | date:'mediumDate' }}
          </span>
          <div class="flex gap-1">
            <button
              class="btn btn-ghost btn-xs"
              (click)="edit.emit(task())"
              aria-label="Edit task"
            >✏️</button>
            <button
              class="btn btn-ghost btn-xs text-error"
              (click)="delete.emit(task().id)"
              aria-label="Delete task"
            >🗑️</button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class TaskCardComponent {
  readonly task = input.required<TaskItem>();
  readonly edit = output<TaskItem>();
  readonly delete = output<string>();
}
