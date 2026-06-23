import { Component, computed, input } from '@angular/core';
import { TaskStatus } from '../../../core/models/task.model';

const BADGE_CLASS: Record<TaskStatus, string> = {
  Todo: 'badge-neutral',
  InProgress: 'badge-warning',
  Done: 'badge-success',
};

const LABEL: Record<TaskStatus, string> = {
  Todo: 'To Do',
  InProgress: 'In Progress',
  Done: 'Done',
};

@Component({
  selector: 'app-task-status-badge',
  standalone: true,
  template: `<span class="badge {{ badgeClass() }}">{{ label() }}</span>`,
})
export class TaskStatusBadgeComponent {
  readonly status = input.required<TaskStatus>();
  readonly badgeClass = computed(() => BADGE_CLASS[this.status()]);
  readonly label = computed(() => LABEL[this.status()]);
}
