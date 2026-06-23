import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TaskItem, TaskRequest, TaskStatus } from '../../../core/models/task.model';
import { TaskService } from '../../../core/services/task.service';
import { ToastService } from '../../../core/services/toast.service';
import { futureDateValidator } from '../../../core/validators/future-date.validator';

@Component({
  selector: 'app-task-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <h3 class="font-bold text-lg mb-4">{{ task() ? 'Edit task' : 'New task' }}</h3>

    <form [formGroup]="form" (ngSubmit)="submit()">
      <!-- Title -->
      <div class="form-control mb-3">
        <label class="label"><span class="label-text">Title *</span></label>
        <input formControlName="title" class="input input-bordered" [class.input-error]="dirty('title')" placeholder="Task title" />
        @if (dirty('title')) {
          <label class="label"><span class="label-text-alt text-error">
            @if (form.get('title')?.errors?.['required']) { Title is required. }
            @else if (form.get('title')?.errors?.['maxlength']) { Max 200 characters. }
          </span></label>
        }
      </div>

      <!-- Description -->
      <div class="form-control mb-3">
        <label class="label"><span class="label-text">Description</span></label>
        <textarea formControlName="description" class="textarea textarea-bordered" rows="3" placeholder="Optional details"></textarea>
      </div>

      <!-- Status -->
      <div class="form-control mb-3">
        <label class="label"><span class="label-text">Status *</span></label>
        <select formControlName="status" class="select select-bordered">
          <option value="Todo">To Do</option>
          <option value="InProgress">In Progress</option>
          <option value="Done">Done</option>
        </select>
      </div>

      <!-- Due date -->
      <div class="form-control mb-5">
        <label class="label"><span class="label-text">Due date *</span></label>
        <input type="date" formControlName="dueDate" class="input input-bordered" [class.input-error]="dirty('dueDate')" />
        @if (dirty('dueDate')) {
          <label class="label"><span class="label-text-alt text-error">
            @if (form.get('dueDate')?.errors?.['required']) { Due date is required. }
            @else if (form.get('dueDate')?.errors?.['pastDate']) { Date must be today or in the future. }
          </span></label>
        }
      </div>

      <div class="modal-action mt-0">
        <button type="button" class="btn btn-ghost" (click)="cancelled.emit()">Cancel</button>
        <button type="submit" class="btn btn-primary" [disabled]="loading()">
          @if (loading()) { <span class="loading loading-spinner loading-sm"></span> }
          {{ task() ? 'Save changes' : 'Create task' }}
        </button>
      </div>
    </form>
  `,
})
export class TaskFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly taskSvc = inject(TaskService);
  private readonly toast = inject(ToastService);

  readonly task = input<TaskItem | null>(null);
  readonly saved = output<TaskItem>();
  readonly cancelled = output<void>();
  readonly loading = signal(false);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    status: ['Todo' as TaskStatus, Validators.required],
    dueDate: ['', [Validators.required, futureDateValidator()]],
  });

  constructor() {
    // Re-populate the form and reset loading every time the task input changes.
    // This covers: first open, switching between tasks, and re-opening after save.
    effect(() => {
      const t = this.task();
      this.form.reset({
        title: t?.title ?? '',
        description: t?.description ?? '',
        status: t?.status ?? 'Todo',
        dueDate: t?.dueDate ?? '',
      });
      this.loading.set(false);
    });
  }

  dirty(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && (c.dirty || c.touched));
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    const payload = this.form.getRawValue() as TaskRequest;
    const t = this.task();
    const call = t
      ? this.taskSvc.updateTask(t.id, payload)
      : this.taskSvc.createTask(payload);

    call.subscribe({
      next: result => {
        this.loading.set(false);
        this.toast.show(t ? 'Task updated.' : 'Task created.', 'success');
        this.saved.emit(result);
      },
      error: () => this.loading.set(false),
    });
  }
}
