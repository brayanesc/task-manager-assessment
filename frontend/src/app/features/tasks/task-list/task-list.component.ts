import {
  Component, ElementRef, OnDestroy, OnInit,
  ViewChild, computed, inject, signal,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subscription, debounceTime, distinctUntilChanged } from 'rxjs';
import { TaskItem } from '../../../core/models/task.model';
import { TaskService } from '../../../core/services/task.service';
import { ToastService } from '../../../core/services/toast.service';
import { TaskCardComponent } from '../task-card/task-card.component';
import { TaskFormComponent } from '../task-form/task-form.component';

const STATUS_OPTIONS = ['', 'Todo', 'InProgress', 'Done'] as const;
const PAGE_SIZE_OPTIONS = [5, 10, 20, 50] as const;
type StatusOption = (typeof STATUS_OPTIONS)[number];

@Component({
  selector: 'app-task-list',
  standalone: true,
  imports: [ReactiveFormsModule, TaskCardComponent, TaskFormComponent],
  template: `
    <!-- Header -->
    <div class="flex items-center justify-between mb-4">
      <h1 class="text-2xl font-bold">My Tasks</h1>
      <button class="btn btn-primary btn-sm" (click)="openCreate()">+ New task</button>
    </div>

    <!-- Search + Status filters -->
    <div class="flex flex-wrap gap-2 mb-2">
      <label class="input input-bordered input-sm flex items-center gap-2 flex-1 min-w-48">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 opacity-50" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
        </svg>
        <input type="text" placeholder="Search tasks…" class="grow bg-transparent outline-none" [formControl]="searchCtrl" />
        @if (searchCtrl.value) {
          <button class="btn btn-ghost btn-xs" (click)="clearSearch()">✕</button>
        }
      </label>
      <div class="join">
        @for (s of statusOptions; track s) {
          <button
            class="join-item btn btn-sm"
            [class.btn-primary]="activeStatus() === s"
            [class.btn-ghost]="activeStatus() !== s"
            (click)="setStatus(s)"
          >{{ s === '' ? 'All' : s === 'InProgress' ? 'In Progress' : s }}</button>
        }
      </div>
    </div>

    <!-- Summary bar -->
    <div class="flex items-center justify-between mb-4 text-sm text-base-content/60">
      <span>
        @if (loading()) { Loading… }
        @else { Showing {{ tasks().length }} of {{ totalCount() }} task{{ totalCount() === 1 ? '' : 's' }} }
      </span>
      <select class="select select-sm select-bordered" (change)="onPageSizeChange($event)">
        @for (n of pageSizeOptions; track n) {
          <option [value]="n" [selected]="n === pageSize()">{{ n }} per page</option>
        }
      </select>
    </div>

    <!-- Spinner -->
    @if (loading()) {
      <div class="flex justify-center py-16">
        <span class="loading loading-spinner loading-lg"></span>
      </div>
    }

    <!-- Empty state -->
    @if (!loading() && tasks().length === 0) {
      <div class="text-center py-16 opacity-50">
        <p class="text-4xl mb-2">📋</p>
        <p>{{ searchCtrl.value || activeStatus() ? 'No tasks match your filters.' : 'No tasks yet. Create your first one!' }}</p>
      </div>
    }

    <!-- Pagination (top) -->
    @if (!loading() && totalPages() > 1) {
      <div class="flex flex-col items-center gap-1 mb-4">
        <div class="join">
          <button class="join-item btn btn-sm" [disabled]="currentPage() === 1" (click)="goToPage(currentPage() - 1)">«</button>
          @for (p of pageNumbers(); track p) {
            <button
              class="join-item btn btn-sm"
              [class.btn-active]="p === currentPage()"
              (click)="goToPage(p)"
            >{{ p }}</button>
          }
          <button class="join-item btn btn-sm" [disabled]="currentPage() === totalPages()" (click)="goToPage(currentPage() + 1)">»</button>
        </div>
        <p class="text-xs opacity-50">Page {{ currentPage() }} of {{ totalPages() }}</p>
      </div>
    }

    <!-- Task grid -->
    @if (!loading() && tasks().length > 0) {
      <div class="grid gap-3 sm:grid-cols-2">
        @for (task of tasks(); track task.id) {
          <app-task-card [task]="task" (edit)="openEdit($event)" (delete)="confirmDelete($event)" />
        }
      </div>
    }

    <!-- Create / Edit modal -->
    <dialog #taskModal class="modal">
      <div class="modal-box w-full max-w-lg">
        <app-task-form [task]="editingTask()" (saved)="onSaved()" (cancelled)="closeModal()" />
      </div>
      <form method="dialog" class="modal-backdrop"><button>close</button></form>
    </dialog>

    <!-- Delete confirm modal -->
    <dialog #deleteModal class="modal">
      <div class="modal-box">
        <h3 class="font-bold text-lg">Delete task?</h3>
        <p class="py-4 opacity-70">This action cannot be undone.</p>
        <div class="modal-action">
          <button class="btn btn-ghost" (click)="deleteModal.close()">Cancel</button>
          <button class="btn btn-error" [disabled]="deleting()" (click)="doDelete()">
            @if (deleting()) { <span class="loading loading-spinner loading-sm"></span> }
            Delete
          </button>
        </div>
      </div>
      <form method="dialog" class="modal-backdrop"><button>close</button></form>
    </dialog>
  `,
})
export class TaskListComponent implements OnInit, OnDestroy {
  @ViewChild('taskModal') taskModal!: ElementRef<HTMLDialogElement>;
  @ViewChild('deleteModal') deleteModal!: ElementRef<HTMLDialogElement>;

  private readonly taskSvc = inject(TaskService);
  private readonly toast   = inject(ToastService);

  readonly statusOptions   = STATUS_OPTIONS;
  readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  readonly searchCtrl = new FormControl('', { nonNullable: true });

  // Filter state
  readonly activeStatus = signal<StatusOption>('');
  readonly pageSize     = signal<number>(10);
  readonly currentPage  = signal(1);

  // Result state
  readonly tasks      = signal<TaskItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading    = signal(false);

  // Derived
  readonly totalPages  = computed(() => Math.ceil(this.totalCount() / this.pageSize()) || 1);
  readonly pageNumbers = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1),
  );

  // Modal state
  readonly editingTask = signal<TaskItem | null>(null);
  readonly deleting    = signal(false);
  private deletingId: string | null = null;

  private searchSub?: Subscription;

  ngOnInit(): void {
    this.fetch();
    this.searchSub = this.searchCtrl.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged())
      .subscribe(() => { this.currentPage.set(1); this.fetch(); });
  }

  ngOnDestroy(): void { this.searchSub?.unsubscribe(); }

  private fetch(): void {
    this.loading.set(true);
    this.taskSvc.getTasks(
      this.currentPage(),
      this.pageSize(),
      this.activeStatus() || undefined,
      this.searchCtrl.value.trim() || undefined,
    ).subscribe({
      next: result => {
        this.tasks.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  goToPage(p: number): void { this.currentPage.set(p); this.fetch(); }

  clearSearch(): void { this.searchCtrl.setValue('', { emitEvent: false }); this.currentPage.set(1); this.fetch(); }

  setStatus(s: StatusOption): void { this.activeStatus.set(s); this.currentPage.set(1); this.fetch(); }

  onPageSizeChange(event: Event): void {
    this.pageSize.set(+(event.target as HTMLSelectElement).value);
    this.currentPage.set(1);
    this.fetch();
  }

  openCreate(): void { this.editingTask.set(null); this.taskModal.nativeElement.showModal(); }
  openEdit(task: TaskItem): void { this.editingTask.set(task); this.taskModal.nativeElement.showModal(); }
  closeModal(): void { this.taskModal.nativeElement.close(); }

  onSaved(): void { this.closeModal(); this.fetch(); }

  confirmDelete(id: string): void { this.deletingId = id; this.deleteModal.nativeElement.showModal(); }

  doDelete(): void {
    if (!this.deletingId) return;
    this.deleting.set(true);
    this.taskSvc.deleteTask(this.deletingId).subscribe({
      next: () => {
        this.toast.show('Task deleted.', 'success');
        this.deleteModal.nativeElement.close();
        this.deleting.set(false);
        this.deletingId = null;
        this.fetch();
      },
      error: () => this.deleting.set(false),
    });
  }
}
