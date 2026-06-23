import {
  Component, computed, inject, OnInit, signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { TaskService } from '../../../core/services/task.service';
import { ToastService } from '../../../core/services/toast.service';
import { TaskItem, TaskPriority, TaskRequest, TaskStatus } from '../../../core/models/task.model';
import { futureDateValidator } from '../../../core/validators/future-date.validator';

// ── Metadata ────────────────────────────────────────────────────────────────

const PRIORITY_META: Record<TaskPriority, { label: string; color: string; bg: string }> = {
  High:   { label: 'High',   color: '#e5484d', bg: 'rgba(229,72,77,.13)' },
  Medium: { label: 'Medium', color: '#d9851f', bg: 'rgba(217,133,31,.14)' },
  Low:    { label: 'Low',    color: '#3e7bfa', bg: 'rgba(62,123,250,.13)' },
};

const STATUS_META: Record<TaskStatus, { label: string; color: string; bg: string }> = {
  Todo:       { label: 'To Do',       color: '#64748b', bg: 'rgba(100,116,139,.14)' },
  InProgress: { label: 'In Progress', color: '#d9851f', bg: 'rgba(217,133,31,.15)' },
  Done:       { label: 'Done',        color: '#1a9d5a', bg: 'rgba(26,157,90,.15)'  },
};

const COLUMNS: { key: TaskStatus; label: string; color: string }[] = [
  { key: 'Todo',       label: 'To Do',       color: '#94a3b8' },
  { key: 'InProgress', label: 'In Progress', color: '#d9851f' },
  { key: 'Done',       label: 'Done',        color: '#1a9d5a' },
];

const NAV_FILTERS: { key: string; label: string; dot: string }[] = [
  { key: '',           label: 'All tasks',    dot: 'var(--accent)' },
  { key: 'Todo',       label: 'To Do',        dot: '#94a3b8' },
  { key: 'InProgress', label: 'In Progress',  dot: '#d9851f' },
  { key: 'Done',       label: 'Done',         dot: '#1a9d5a' },
];

// ── Component ────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-task-shell',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe],
  providers: [DatePipe],
  template: `
<div [class.dark]="isDark()" style="display:flex;height:100vh;overflow:hidden;background:var(--bg);color:var(--text);">

  <!-- ── Sidebar ─────────────────────────────────────────────────────────── -->
  <aside style="
    width:248px;flex-shrink:0;background:var(--sidebar);
    border-right:1px solid var(--border);
    display:flex;flex-direction:column;overflow:hidden;
  ">
    <!-- Logo -->
    <div style="padding:20px 20px 16px;display:flex;align-items:center;gap:10px;">
      <div style="width:32px;height:32px;border-radius:8px;background:var(--accent);
        display:flex;align-items:center;justify-content:center;flex-shrink:0;">
        <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5"
          stroke-linecap="round" stroke-linejoin="round">
          <polyline points="20 6 9 17 4 12"/>
        </svg>
      </div>
      <span style="font-size:15px;font-weight:700;color:var(--text);">Task Manager</span>
    </div>

    <!-- Workspace label -->
    <div style="padding:0 16px 8px;font-size:11px;font-weight:600;color:var(--faint);text-transform:uppercase;letter-spacing:.08em;">
      Workspace
    </div>

    <!-- Nav filters -->
    <nav style="padding:0 8px;flex:1;overflow-y:auto;">
      @for (nav of navFilters; track nav.key) {
        <button
          (click)="filter.set(nav.key)"
          style="
            width:100%;display:flex;align-items:center;gap:10px;
            padding:8px 10px;border-radius:7px;border:none;cursor:pointer;
            font-size:13px;font-weight:500;font-family:inherit;
            text-align:left;transition:background .12s;
          "
          [style.background]="filter() === nav.key ? 'var(--accent-soft)' : 'transparent'"
          [style.color]="filter() === nav.key ? 'var(--accent)' : 'var(--dim)'"
        >
          <span style="width:7px;height:7px;border-radius:50%;flex-shrink:0;"
            [style.background]="nav.dot"></span>
          <span style="flex:1;">{{ nav.label }}</span>
          <span style="
            font-size:11px;font-weight:600;padding:1px 6px;border-radius:10px;
            background:var(--border);color:var(--dim);
          ">{{ countFor(nav.key) }}</span>
        </button>
      }
    </nav>

    <!-- Spacer + Theme toggle -->
    <div style="padding:12px 8px 0;">
      <button
        (click)="toggleTheme()"
        style="
          width:100%;display:flex;align-items:center;gap:10px;
          padding:8px 10px;border-radius:7px;border:none;cursor:pointer;
          font-size:13px;font-weight:500;color:var(--dim);background:transparent;
          font-family:inherit;
        "
      >
        @if (isDark()) {
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="5"/>
            <line x1="12" y1="1" x2="12" y2="3"/>
            <line x1="12" y1="21" x2="12" y2="23"/>
            <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/>
            <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
            <line x1="1" y1="12" x2="3" y2="12"/>
            <line x1="21" y1="12" x2="23" y2="12"/>
            <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/>
            <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
          </svg>
          Light mode
        } @else {
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linecap="round" stroke-linejoin="round">
            <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
          </svg>
          Dark mode
        }
      </button>
    </div>

    <!-- User footer -->
    <div style="
      padding:12px 16px 16px;border-top:1px solid var(--border);
      display:flex;align-items:center;gap:10px;
    ">
      <div style="
        width:32px;height:32px;border-radius:50%;background:var(--accent-soft);
        display:flex;align-items:center;justify-content:center;
        font-size:13px;font-weight:700;color:var(--accent);flex-shrink:0;
      ">{{ userInitial() }}</div>
      <span style="flex:1;font-size:13px;color:var(--dim);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">
        {{ currentUser()?.email }}
      </span>
      <button
        (click)="logout()"
        title="Sign out"
        style="
          background:none;border:none;cursor:pointer;
          color:var(--faint);padding:4px;border-radius:5px;
          display:flex;align-items:center;
        "
      >
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          stroke-linecap="round" stroke-linejoin="round">
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
          <polyline points="16 17 21 12 16 7"/>
          <line x1="21" y1="12" x2="9" y2="12"/>
        </svg>
      </button>
    </div>
  </aside>

  <!-- ── Main area ────────────────────────────────────────────────────────── -->
  <div style="flex:1;display:flex;flex-direction:column;overflow:hidden;">

    <!-- Header -->
    <header style="
      padding:20px 28px 0;display:flex;align-items:flex-start;gap:16px;
      border-bottom:1px solid var(--border);background:var(--surface);
      padding-bottom:16px;
    ">
      <div style="flex:1;">
        <h1 style="margin:0 0 2px;font-size:20px;font-weight:800;color:var(--text);">
          {{ pageTitle() }}
        </h1>
        <p style="margin:0;font-size:13px;color:var(--dim);">
          {{ visibleTasks().length }} task{{ visibleTasks().length === 1 ? '' : 's' }}
        </p>
      </div>

      <!-- Search -->
      <div style="position:relative;flex-shrink:0;">
        <svg style="position:absolute;left:10px;top:50%;transform:translateY(-50%);pointer-events:none;"
          width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="var(--faint)" stroke-width="2"
          stroke-linecap="round" stroke-linejoin="round">
          <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
        </svg>
        <input
          type="search"
          placeholder="Search tasks…"
          [value]="search()"
          (input)="search.set($any($event.target).value)"
          style="
            padding:8px 12px 8px 32px;border-radius:8px;font-size:13px;width:200px;
            border:1px solid var(--border-strong);background:var(--surface-2);color:var(--text);
            outline:none;font-family:inherit;
          "
        />
      </div>

      <!-- View toggles -->
      <div style="display:flex;gap:4px;padding:3px;background:var(--surface-2);border-radius:8px;border:1px solid var(--border);">
        @for (v of views; track v.key) {
          <button
            (click)="view.set(v.key)"
            [title]="v.label"
            style="
              padding:5px 10px;border-radius:6px;border:none;cursor:pointer;
              font-size:12px;font-weight:600;font-family:inherit;transition:all .12s;
            "
            [style.background]="view() === v.key ? 'var(--surface)' : 'transparent'"
            [style.color]="view() === v.key ? 'var(--text)' : 'var(--faint)'"
            [style.box-shadow]="view() === v.key ? 'var(--shadow)' : 'none'"
          >{{ v.label }}</button>
        }
      </div>

      <!-- Reload -->
      <button
        (click)="reload()"
        title="Refresh"
        style="
          padding:7px;border-radius:8px;border:1px solid var(--border);
          background:var(--surface-2);color:var(--dim);cursor:pointer;
          display:flex;align-items:center;transition:color .12s;
        "
      >
        <svg [style.animation]="reloading() ? 'spin 1s linear infinite' : 'none'"
          width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          stroke-linecap="round" stroke-linejoin="round">
          <polyline points="23 4 23 10 17 10"/>
          <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/>
        </svg>
      </button>

      <!-- New task -->
      <button
        (click)="openCreate()"
        style="
          padding:8px 14px;border-radius:8px;border:none;cursor:pointer;
          background:var(--accent);color:var(--accent-fg);
          font-size:13px;font-weight:600;font-family:inherit;
          display:flex;align-items:center;gap:6px;
          white-space:nowrap;
        "
      >
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"
          stroke-linecap="round" stroke-linejoin="round">
          <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
        New task
      </button>
    </header>

    <!-- Content -->
    <div style="flex:1;overflow-y:auto;padding:24px 28px;">

      <!-- Loading skeletons -->
      @if (loading()) {
        <div style="display:flex;flex-direction:column;gap:10px;">
          @for (s of [1,2,3,4,5,6]; track s) {
            <div style="
              height:64px;border-radius:10px;
              background:var(--surface);border:1px solid var(--border);
              animation:pulse 1.5s ease-in-out infinite;
            "></div>
          }
        </div>
      }

      <!-- Empty state -->
      @else if (isEmpty()) {
        <div style="
          display:flex;flex-direction:column;align-items:center;justify-content:center;
          height:320px;gap:12px;
        ">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="var(--faint)" stroke-width="1.5"
            stroke-linecap="round" stroke-linejoin="round">
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
            <line x1="9" y1="9" x2="15" y2="9"/><line x1="9" y1="13" x2="15" y2="13"/>
            <line x1="9" y1="17" x2="11" y2="17"/>
          </svg>
          <p style="font-size:15px;font-weight:600;color:var(--dim);margin:0;">No tasks found</p>
          <p style="font-size:13px;color:var(--faint);margin:0;">
            {{ search() ? 'Try a different search term.' : 'Create your first task to get started.' }}
          </p>
          @if (!search()) {
            <button (click)="openCreate()" style="
              margin-top:8px;padding:8px 18px;border-radius:8px;border:none;cursor:pointer;
              background:var(--accent);color:var(--accent-fg);font-size:13px;font-weight:600;font-family:inherit;
            ">+ New task</button>
          }
        </div>
      }

      <!-- Board view -->
      @else if (view() === 'board') {
        <div style="display:flex;gap:16px;align-items:flex-start;overflow-x:auto;">
          @for (col of boardColumns(); track col.key) {
            <div
              style="
                width:300px;flex-shrink:0;background:var(--surface-2);
                border-radius:12px;border:1px solid var(--border);padding:12px;
              "
              (dragover)="$event.preventDefault()"
              (drop)="onDrop($event, col.key)"
              [id]="'col-' + col.key"
            >
              <!-- Column header -->
              <div style="display:flex;align-items:center;gap:8px;margin-bottom:12px;">
                <span style="width:8px;height:8px;border-radius:50%;flex-shrink:0;"
                  [style.background]="col.color"></span>
                <span style="font-size:13px;font-weight:700;color:var(--text);flex:1;">{{ col.label }}</span>
                <span style="font-size:12px;color:var(--dim);font-weight:600;">{{ col.count }}</span>
                <button
                  (click)="openCreateForStatus(col.key)"
                  style="
                    background:none;border:none;cursor:pointer;color:var(--faint);
                    padding:2px 4px;border-radius:4px;font-size:16px;line-height:1;
                  "
                >+</button>
              </div>

              <!-- Cards -->
              <div style="display:flex;flex-direction:column;gap:8px;"
                [style.min-height]="col.tasks.length === 0 ? '120px' : null">
                @if (col.tasks.length === 0) {
                  <div style="
                    flex:1;min-height:120px;border-radius:8px;
                    border:2px dashed var(--border-strong);
                    display:flex;align-items:center;justify-content:center;
                    color:var(--faint);font-size:12px;font-weight:500;
                  ">Drop here</div>
                }
                @for (task of col.tasks; track task.id) {
                  <div
                    draggable="true"
                    (dragstart)="onDragStart($event, task.id)"
                    (click)="detailTask.set(task)"
                    style="
                      background:var(--surface);border:1px solid var(--border);
                      border-radius:10px;padding:12px;cursor:grab;
                      transition:box-shadow .12s;
                    "
                    (mouseenter)="$any($event.target).style.boxShadow='var(--shadow)'"
                    (mouseleave)="$any($event.target).style.boxShadow='none'"
                  >
                    <!-- Priority badge -->
                    <span style="
                      display:inline-block;font-size:11px;font-weight:600;
                      padding:2px 7px;border-radius:5px;margin-bottom:8px;
                    "
                      [style.color]="priorityMeta(task.priority).color"
                      [style.background]="priorityMeta(task.priority).bg"
                    >{{ priorityMeta(task.priority).label }}</span>
                    <!-- Title -->
                    <p style="font-size:13px;font-weight:600;color:var(--text);margin:0 0 4px;
                      overflow:hidden;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;">
                      {{ task.title }}
                    </p>
                    <!-- Description -->
                    @if (task.description) {
                      <p style="font-size:12px;color:var(--dim);margin:0 0 8px;
                        overflow:hidden;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;">
                        {{ task.description }}
                      </p>
                    }
                    <!-- Due date -->
                    <div style="display:flex;align-items:center;gap:5px;">
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none"
                        [attr.stroke]="isOverdue(task) ? '#e5484d' : 'var(--faint)'"
                        stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                        <line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/>
                        <line x1="3" y1="10" x2="21" y2="10"/>
                      </svg>
                      <span class="mono" style="font-size:11px;"
                        [style.color]="isOverdue(task) ? '#e5484d' : 'var(--faint)'">
                        {{ dueDateDisplay(task) }}
                      </span>
                    </div>
                  </div>
                }
              </div>

              <!-- Load more -->
              @if (col.hasMore) {
                <button
                  (click)="loadMore(col.key)"
                  style="
                    width:100%;margin-top:10px;padding:7px;border-radius:8px;
                    border:1px dashed var(--border-strong);background:transparent;
                    font-size:12px;font-weight:600;color:var(--dim);cursor:pointer;font-family:inherit;
                  "
                >Load {{ col.count - col.tasks.length }} more</button>
              }
            </div>
          }
        </div>
      }

      <!-- List view -->
      @else if (view() === 'list') {
        <div style="background:var(--surface);border:1px solid var(--border);border-radius:12px;overflow:hidden;">
          <!-- Header row -->
          <div style="
            display:grid;grid-template-columns:1fr 130px 120px 130px 44px;
            padding:10px 16px;border-bottom:1px solid var(--border);
            font-size:11px;font-weight:700;color:var(--faint);text-transform:uppercase;letter-spacing:.07em;
            align-items:center;
          ">
            @for (hdr of listHeaders; track hdr.col) {
              <button
                (click)="cycleSort(hdr.col)"
                style="
                  background:none;border:none;cursor:pointer;padding:0;
                  font:inherit;font-size:11px;font-weight:700;
                  letter-spacing:.07em;text-transform:uppercase;
                  display:flex;align-items:center;gap:4px;
                  text-align:left;
                "
                [style.color]="sortCol() === hdr.col ? 'var(--accent)' : 'var(--faint)'"
              >
                {{ hdr.label }}
                <span style="font-size:10px;opacity:.8;">{{ sortIcon(hdr.col) }}</span>
              </button>
            }
            <span></span>
          </div>
          @for (task of sortedPagedTasks(); track task.id) {
            <div
              (click)="detailTask.set(task)"
              style="
                display:grid;grid-template-columns:1fr 130px 120px 130px 44px;
                padding:12px 16px;border-bottom:1px solid var(--border);
                cursor:pointer;transition:background .1s;align-items:center;
              "
              (mouseenter)="$any($event.currentTarget).style.background='var(--surface-2)'"
              (mouseleave)="$any($event.currentTarget).style.background='transparent'"
            >
              <div style="min-width:0;">
                <p style="font-size:13px;font-weight:600;color:var(--text);margin:0;
                  overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">{{ task.title }}</p>
                @if (task.description) {
                  <p style="font-size:12px;color:var(--dim);margin:2px 0 0;
                    overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">{{ task.description }}</p>
                }
              </div>
              <span style="font-size:12px;font-weight:600;padding:3px 8px;border-radius:5px;width:fit-content;"
                [style.color]="priorityMeta(task.priority).color"
                [style.background]="priorityMeta(task.priority).bg"
              >{{ priorityMeta(task.priority).label }}</span>
              <span style="font-size:12px;font-weight:600;padding:3px 8px;border-radius:5px;width:fit-content;"
                [style.color]="statusMeta(task.status).color"
                [style.background]="statusMeta(task.status).bg"
              >{{ statusMeta(task.status).label }}</span>
              <span class="mono" style="font-size:12px;"
                [style.color]="isOverdue(task) ? '#e5484d' : 'var(--dim)'">
                {{ dueDateDisplay(task) }}
              </span>
              <button
                (click)="$event.stopPropagation(); openEdit(task)"
                style="
                  background:none;border:none;cursor:pointer;color:var(--faint);
                  padding:6px;border-radius:6px;display:flex;align-items:center;
                "
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                  stroke-linecap="round" stroke-linejoin="round">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                </svg>
              </button>
            </div>
          }
        </div>
      }

      <!-- Grid view -->
      @else {
        <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:14px;">
          @for (task of pagedTasks(); track task.id) {
            <div
              (click)="detailTask.set(task)"
              style="
                background:var(--surface);border:1px solid var(--border);border-radius:12px;
                padding:14px;cursor:pointer;display:flex;flex-direction:column;gap:10px;
                transition:box-shadow .12s;
              "
              (mouseenter)="$any($event.currentTarget).style.boxShadow='var(--shadow)'"
              (mouseleave)="$any($event.currentTarget).style.boxShadow='none'"
            >
              <!-- Top row: status + priority -->
              <div style="display:flex;align-items:center;gap:6px;">
                <span style="font-size:11px;font-weight:600;padding:2px 7px;border-radius:5px;"
                  [style.color]="statusMeta(task.status).color"
                  [style.background]="statusMeta(task.status).bg"
                >{{ statusMeta(task.status).label }}</span>
                <span style="font-size:11px;font-weight:600;padding:2px 7px;border-radius:5px;"
                  [style.color]="priorityMeta(task.priority).color"
                  [style.background]="priorityMeta(task.priority).bg"
                >{{ priorityMeta(task.priority).label }}</span>
              </div>
              <!-- Title -->
              <p style="font-size:14px;font-weight:700;color:var(--text);margin:0;line-height:1.4;">
                {{ task.title }}
              </p>
              <!-- Description -->
              @if (task.description) {
                <p style="font-size:12px;color:var(--dim);margin:0;line-height:1.5;
                  overflow:hidden;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;">
                  {{ task.description }}
                </p>
              }
              <!-- Due date -->
              <div style="margin-top:auto;padding-top:10px;border-top:1px solid var(--border);
                display:flex;align-items:center;gap:6px;">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none"
                  [attr.stroke]="isOverdue(task) ? '#e5484d' : 'var(--faint)'"
                  stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                  <line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/>
                  <line x1="3" y1="10" x2="21" y2="10"/>
                </svg>
                <span class="mono" style="font-size:11px;"
                  [style.color]="isOverdue(task) ? '#e5484d' : 'var(--faint)'">
                  {{ dueDateDisplay(task) }}
                </span>
              </div>
            </div>
          }
        </div>
      }

      <!-- Paginator -->
      @if (showPager()) {
        <div style="
          display:flex;align-items:center;gap:12px;margin-top:20px;
          font-size:13px;color:var(--dim);flex-wrap:wrap;
        ">
          <span>Showing {{ pagerStart() }}–{{ pagerEnd() }} of {{ visibleTasks().length }}</span>
          <div style="flex:1;"></div>
          <button
            (click)="page.set(page() - 1)"
            [disabled]="page() <= 1"
            style="
              padding:6px 12px;border-radius:7px;border:1px solid var(--border);
              background:var(--surface);color:var(--dim);cursor:pointer;font-size:12px;font-family:inherit;
            "
            [style.opacity]="page() <= 1 ? '0.4' : '1'"
          >Prev</button>
          @for (p of pageNumbers(); track p) {
            <button
              (click)="page.set(p)"
              style="
                width:32px;height:32px;border-radius:7px;border:1px solid var(--border);
                font-size:12px;font-weight:600;cursor:pointer;font-family:inherit;
              "
              [style.background]="p === page() ? 'var(--accent)' : 'var(--surface)'"
              [style.color]="p === page() ? 'var(--accent-fg)' : 'var(--dim)'"
              [style.border-color]="p === page() ? 'var(--accent)' : 'var(--border)'"
            >{{ p }}</button>
          }
          <button
            (click)="page.set(page() + 1)"
            [disabled]="page() >= totalPages()"
            style="
              padding:6px 12px;border-radius:7px;border:1px solid var(--border);
              background:var(--surface);color:var(--dim);cursor:pointer;font-size:12px;font-family:inherit;
            "
            [style.opacity]="page() >= totalPages() ? '0.4' : '1'"
          >Next</button>
          <select
            [value]="perPage()"
            (change)="onPerPageChange($event)"
            style="
              padding:6px 8px;border-radius:7px;border:1px solid var(--border);
              background:var(--surface);color:var(--dim);font-size:12px;font-family:inherit;cursor:pointer;
            "
          >
            <option value="6">6 per page</option>
            <option value="9">9 per page</option>
            <option value="12">12 per page</option>
            <option value="24">24 per page</option>
          </select>
        </div>
      }
    </div>
  </div>

  <!-- ── Task Detail Panel ──────────────────────────────────────────────── -->
  @if (detailTask()) {
    <div style="
      width:420px;flex-shrink:0;background:var(--surface);
      border-left:1px solid var(--border);
      display:flex;flex-direction:column;overflow:hidden;
      animation:slideIn .2s ease;
    ">
      <!-- Panel header -->
      <div style="
        padding:16px 20px;border-bottom:1px solid var(--border);
        display:flex;align-items:center;gap:8px;
      ">
        <span style="font-size:11px;font-weight:600;padding:2px 8px;border-radius:5px;"
          [style.color]="priorityMeta(detailTask()!.priority).color"
          [style.background]="priorityMeta(detailTask()!.priority).bg"
        >{{ priorityMeta(detailTask()!.priority).label }}</span>
        <div style="flex:1;"></div>
        <button (click)="openEdit(detailTask()!)"
          style="
            background:none;border:1px solid var(--border);cursor:pointer;color:var(--dim);
            padding:6px;border-radius:7px;display:flex;align-items:center;
          "
          title="Edit">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linecap="round" stroke-linejoin="round">
            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
          </svg>
        </button>
        <button (click)="deleteTask(detailTask()!)"
          [disabled]="deleting()"
          style="
            background:none;border:1px solid var(--border);cursor:pointer;color:#e5484d;
            padding:6px;border-radius:7px;display:flex;align-items:center;
          "
          title="Delete">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linecap="round" stroke-linejoin="round">
            <polyline points="3 6 5 6 21 6"/>
            <path d="M19 6l-1 14H6L5 6"/>
            <path d="M10 11v6"/><path d="M14 11v6"/>
            <path d="M9 6V4h6v2"/>
          </svg>
        </button>
        <button (click)="detailTask.set(null)"
          style="
            background:none;border:1px solid var(--border);cursor:pointer;color:var(--faint);
            padding:6px;border-radius:7px;display:flex;align-items:center;
          "
          title="Close">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
            stroke-linecap="round" stroke-linejoin="round">
            <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
          </svg>
        </button>
      </div>

      <!-- Panel body -->
      <div style="flex:1;overflow-y:auto;padding:20px;">
        <h2 style="font-size:18px;font-weight:800;color:var(--text);margin:0 0 16px;line-height:1.3;">
          {{ detailTask()!.title }}
        </h2>

        <!-- Status toggles -->
        <div style="margin-bottom:20px;">
          <p style="font-size:11px;font-weight:700;color:var(--faint);text-transform:uppercase;
            letter-spacing:.07em;margin:0 0 8px;">Status</p>
          <div style="display:flex;gap:6px;">
            @for (col of columns; track col.key) {
              <button
                (click)="changeStatus(detailTask()!, col.key)"
                style="
                  padding:5px 12px;border-radius:7px;font-size:12px;font-weight:600;
                  cursor:pointer;font-family:inherit;border:1px solid var(--border);
                  transition:all .12s;
                "
                [style.background]="detailTask()!.status === col.key ? statusMeta(col.key).bg : 'transparent'"
                [style.color]="detailTask()!.status === col.key ? statusMeta(col.key).color : 'var(--dim)'"
                [style.border-color]="detailTask()!.status === col.key ? statusMeta(col.key).color : 'var(--border)'"
              >{{ col.label }}</button>
            }
          </div>
        </div>

        <!-- Description -->
        @if (detailTask()!.description) {
          <div style="margin-bottom:20px;">
            <p style="font-size:11px;font-weight:700;color:var(--faint);text-transform:uppercase;
              letter-spacing:.07em;margin:0 0 8px;">Description</p>
            <p style="font-size:13px;color:var(--dim);margin:0;line-height:1.6;white-space:pre-wrap;">
              {{ detailTask()!.description }}
            </p>
          </div>
        }

        <!-- Due date + ID -->
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
          <div>
            <p style="font-size:11px;font-weight:700;color:var(--faint);text-transform:uppercase;
              letter-spacing:.07em;margin:0 0 6px;">Due date</p>
            <p class="mono" style="font-size:13px;margin:0;"
              [style.color]="isOverdue(detailTask()!) ? '#e5484d' : 'var(--text)'">
              {{ dueDateDisplay(detailTask()!) }}
            </p>
          </div>
          <div>
            <p style="font-size:11px;font-weight:700;color:var(--faint);text-transform:uppercase;
              letter-spacing:.07em;margin:0 0 6px;">Task ID</p>
            <p class="mono" style="font-size:11px;color:var(--faint);margin:0;
              overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">
              {{ detailTask()!.id }}
            </p>
          </div>
        </div>
      </div>
    </div>
  }

  <!-- ── Modal ──────────────────────────────────────────────────────────── -->
  @if (modal()) {
    <div
      (click)="closeModal()"
      style="
        position:fixed;inset:0;background:rgba(0,0,0,.45);
        display:flex;align-items:center;justify-content:center;
        z-index:1000;backdrop-filter:blur(4px);padding:16px;
      "
    >
      <div
        (click)="$event.stopPropagation()"
        style="
          background:var(--surface);border-radius:14px;
          width:100%;max-width:480px;box-shadow:var(--shadow);
          display:flex;flex-direction:column;overflow:hidden;
          max-height:90vh;
        "
      >
        <!-- Modal header -->
        <div style="
          padding:18px 20px;border-bottom:1px solid var(--border);
          display:flex;align-items:center;
        ">
          <h3 style="margin:0;font-size:16px;font-weight:700;color:var(--text);">
            {{ editingTask() ? 'Edit task' : 'New task' }}
          </h3>
          <div style="flex:1;"></div>
          <button (click)="closeModal()"
            style="background:none;border:none;cursor:pointer;color:var(--faint);padding:4px;">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
              stroke-linecap="round" stroke-linejoin="round">
              <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
          </button>
        </div>

        <!-- Modal form -->
        <form [formGroup]="taskForm" (ngSubmit)="saveTask()" style="overflow-y:auto;">
          <div style="padding:20px;display:flex;flex-direction:column;gap:14px;">

            <div>
              <label style="display:block;font-size:12px;font-weight:600;color:var(--text);margin-bottom:5px;">
                Title <span style="color:#e5484d;">*</span>
              </label>
              <input
                formControlName="title"
                type="text"
                placeholder="What needs to be done?"
                style="
                  width:100%;padding:9px 12px;border-radius:8px;font-size:13px;
                  border:1px solid var(--border-strong);background:var(--surface-2);color:var(--text);
                  outline:none;font-family:inherit;
                "
                [style.border-color]="formDirty('title') ? '#e5484d' : 'var(--border-strong)'"
              />
              @if (formDirty('title')) {
                <p style="font-size:11px;color:#e5484d;margin:4px 0 0;">
                  @if (taskForm.get('title')?.errors?.['required']) { Title is required. }
                  @else if (taskForm.get('title')?.errors?.['maxlength']) { Max 120 characters. }
                </p>
              }
            </div>

            <div>
              <label style="display:block;font-size:12px;font-weight:600;color:var(--text);margin-bottom:5px;">
                Description
              </label>
              <textarea
                formControlName="description"
                placeholder="Add details…"
                rows="3"
                style="
                  width:100%;padding:9px 12px;border-radius:8px;font-size:13px;
                  border:1px solid var(--border-strong);background:var(--surface-2);color:var(--text);
                  outline:none;font-family:inherit;resize:vertical;
                "
              ></textarea>
            </div>

            <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px;">
              <div>
                <label style="display:block;font-size:12px;font-weight:600;color:var(--text);margin-bottom:5px;">
                  Status
                </label>
                <select
                  formControlName="status"
                  style="
                    width:100%;padding:9px 12px;border-radius:8px;font-size:13px;
                    border:1px solid var(--border-strong);background:var(--surface-2);color:var(--text);
                    outline:none;font-family:inherit;cursor:pointer;
                  "
                >
                  <option value="Todo">To Do</option>
                  <option value="InProgress">In Progress</option>
                  <option value="Done">Done</option>
                </select>
              </div>

              <div>
                <label style="display:block;font-size:12px;font-weight:600;color:var(--text);margin-bottom:5px;">
                  Priority
                </label>
                <select
                  formControlName="priority"
                  style="
                    width:100%;padding:9px 12px;border-radius:8px;font-size:13px;
                    border:1px solid var(--border-strong);background:var(--surface-2);color:var(--text);
                    outline:none;font-family:inherit;cursor:pointer;
                  "
                >
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                </select>
              </div>
            </div>

            <div>
              <label style="display:block;font-size:12px;font-weight:600;color:var(--text);margin-bottom:5px;">
                Due date <span style="color:#e5484d;">*</span>
              </label>
              <input
                formControlName="dueDate"
                type="date"
                style="
                  width:100%;padding:9px 12px;border-radius:8px;font-size:13px;
                  border:1px solid var(--border-strong);background:var(--surface-2);color:var(--text);
                  outline:none;font-family:inherit;
                "
                [style.border-color]="formDirty('dueDate') ? '#e5484d' : 'var(--border-strong)'"
              />
              @if (formDirty('dueDate')) {
                <p style="font-size:11px;color:#e5484d;margin:4px 0 0;">
                  @if (taskForm.get('dueDate')?.errors?.['required']) { Due date is required. }
                  @else if (taskForm.get('dueDate')?.errors?.['pastDate']) { Date must be today or future. }
                </p>
              }
            </div>
          </div>

          <!-- Modal footer -->
          <div style="
            padding:14px 20px;border-top:1px solid var(--border);
            display:flex;justify-content:flex-end;gap:10px;
          ">
            <button
              type="button"
              (click)="closeModal()"
              style="
                padding:8px 16px;border-radius:8px;border:1px solid var(--border);
                background:transparent;color:var(--dim);font-size:13px;font-weight:600;
                cursor:pointer;font-family:inherit;
              "
            >Cancel</button>
            <button
              type="submit"
              [disabled]="saving()"
              style="
                padding:8px 20px;border-radius:8px;border:none;cursor:pointer;
                background:var(--accent);color:var(--accent-fg);
                font-size:13px;font-weight:600;font-family:inherit;
              "
              [style.opacity]="saving() ? '0.7' : '1'"
            >
              {{ saving() ? 'Saving…' : 'Save' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  }
</div>

<style>
@keyframes slideIn {
  from { transform: translateX(30px); opacity: 0; }
  to   { transform: translateX(0);    opacity: 1; }
}
@keyframes pulse {
  0%, 100% { opacity: 1; }
  50%       { opacity: 0.4; }
}
@keyframes spin {
  from { transform: rotate(0deg); }
  to   { transform: rotate(360deg); }
}
</style>
  `,
})
export class TaskShellComponent implements OnInit {
  // ── Injected services ──────────────────────────────────────────────────────
  private readonly taskSvc  = inject(TaskService);
  private readonly auth     = inject(AuthService);
  private readonly toast    = inject(ToastService);
  private readonly router   = inject(Router);
  private readonly fb       = inject(FormBuilder);
  private readonly datePipe = inject(DatePipe);

  // ── Exposed metadata constants ─────────────────────────────────────────────
  readonly navFilters  = NAV_FILTERS;
  readonly columns     = COLUMNS;
  readonly listHeaders: { col: 'title' | 'priority' | 'status' | 'dueDate'; label: string }[] = [
    { col: 'title',    label: 'Task'     },
    { col: 'priority', label: 'Priority' },
    { col: 'status',   label: 'Status'   },
    { col: 'dueDate',  label: 'Due'      },
  ];
  readonly views      = [
    { key: 'board' as const, label: 'Board' },
    { key: 'list'  as const, label: 'List'  },
    { key: 'grid'  as const, label: 'Grid'  },
  ];

  // ── Signals ────────────────────────────────────────────────────────────────
  readonly theme      = signal<'light' | 'dark'>('light');
  readonly filter     = signal<string>('');
  readonly view       = signal<'board' | 'list' | 'grid'>('board');
  readonly search     = signal('');
  readonly tasks      = signal<TaskItem[]>([]);
  readonly loading    = signal(true);
  readonly page       = signal(1);
  readonly perPage    = signal(6);
  readonly modal      = signal(false);
  readonly editingTask = signal<TaskItem | null>(null);
  readonly detailTask  = signal<TaskItem | null>(null);
  readonly colShown   = signal<Record<string, number>>({ Todo: 8, InProgress: 8, Done: 8 });
  readonly saving     = signal(false);
  readonly deleting   = signal(false);
  readonly reloading  = signal(false);
  readonly sortCol    = signal<'title' | 'priority' | 'status' | 'dueDate' | null>(null);
  readonly sortDir    = signal<'asc' | 'desc'>('asc');

  // ── Computed ───────────────────────────────────────────────────────────────
  readonly isDark      = computed(() => this.theme() === 'dark');
  readonly currentUser = computed(() => this.auth.currentUser());
  readonly userInitial = computed(() => {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase();
  });

  readonly visibleTasks = computed(() => {
    let list = this.tasks();
    if (this.filter()) list = list.filter(t => t.status === this.filter());
    const q = this.search().trim().toLowerCase();
    if (q) list = list.filter(t =>
      t.title.toLowerCase().includes(q) || t.description.toLowerCase().includes(q));
    return list;
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.visibleTasks().length / this.perPage())));

  readonly pagedTasks = computed(() => {
    const start = (this.page() - 1) * this.perPage();
    return this.visibleTasks().slice(start, start + this.perPage());
  });

  readonly sortedPagedTasks = computed(() => {
    const tasks = this.pagedTasks();
    const col   = this.sortCol();
    const dir   = this.sortDir();
    if (!col) return tasks;

    const PRIORITY_ORDER: Record<TaskPriority, number> = { Low: 0, Medium: 1, High: 2 };
    const STATUS_ORDER:   Record<TaskStatus,   number> = { Todo: 0, InProgress: 1, Done: 2 };

    return [...tasks].sort((a, b) => {
      let cmp = 0;
      if (col === 'title')    cmp = a.title.localeCompare(b.title);
      if (col === 'priority') cmp = PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority];
      if (col === 'status')   cmp = STATUS_ORDER[a.status]     - STATUS_ORDER[b.status];
      if (col === 'dueDate')  cmp = a.dueDate.localeCompare(b.dueDate);
      return dir === 'asc' ? cmp : -cmp;
    });
  });

  readonly boardColumns = computed(() =>
    COLUMNS.map(col => {
      const colTasks = this.visibleTasks().filter(t => t.status === col.key);
      const shown = this.colShown()[col.key] ?? 8;
      return {
        ...col,
        count: colTasks.length,
        tasks: colTasks.slice(0, shown),
        hasMore: colTasks.length > shown,
      };
    }));

  readonly isEmpty    = computed(() => !this.loading() && this.visibleTasks().length === 0);
  readonly showPager  = computed(() =>
    !this.loading() && !this.isEmpty() && this.view() !== 'board');

  readonly pagerStart = computed(() =>
    Math.min((this.page() - 1) * this.perPage() + 1, this.visibleTasks().length));
  readonly pagerEnd   = computed(() =>
    Math.min(this.page() * this.perPage(), this.visibleTasks().length));

  readonly pageNumbers = computed(() => {
    const total = this.totalPages();
    return Array.from({ length: total }, (_, i) => i + 1);
  });

  readonly pageTitle = computed(() => {
    const f = this.filter();
    if (!f) return 'All tasks';
    return NAV_FILTERS.find(n => n.key === f)?.label ?? 'Tasks';
  });

  // ── Form ───────────────────────────────────────────────────────────────────
  readonly taskForm = this.fb.nonNullable.group({
    title:       ['', [Validators.required, Validators.maxLength(120)]],
    description: [''],
    status:      ['Todo' as TaskStatus],
    priority:    ['Medium' as TaskPriority],
    dueDate:     ['', [Validators.required, futureDateValidator()]],
  });

  // ── Drag state ─────────────────────────────────────────────────────────────
  private draggedId: string | null = null;

  // ── Lifecycle ──────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadAllTasks();
  }

  // ── Data loading ───────────────────────────────────────────────────────────
  private loadAllTasks(): void {
    this.loading.set(true);
    this.taskSvc.getTasks(1, 200).subscribe({
      next: res => {
        this.tasks.set(res.items);
        this.loading.set(false);
      },
      error: () => {
        this.toast.show('Failed to load tasks.', 'error');
        this.loading.set(false);
      },
    });
  }

  reload(): void {
    this.reloading.set(true);
    this.taskSvc.getTasks(1, 200).subscribe({
      next: res => {
        this.tasks.set(res.items);
        this.reloading.set(false);
      },
      error: () => {
        this.toast.show('Reload failed.', 'error');
        this.reloading.set(false);
      },
    });
  }

  // ── Navigation / filters ──────────────────────────────────────────────────
  countFor(key: string): number {
    if (!key) return this.tasks().length;
    return this.tasks().filter(t => t.status === key).length;
  }

  // ── Theme ──────────────────────────────────────────────────────────────────
  toggleTheme(): void {
    this.theme.update(t => t === 'light' ? 'dark' : 'light');
  }

  // ── Auth ───────────────────────────────────────────────────────────────────
  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  // ── Modal ──────────────────────────────────────────────────────────────────
  openCreate(): void {
    this.editingTask.set(null);
    this.taskForm.reset({
      title: '', description: '', status: 'Todo', priority: 'Medium', dueDate: '',
    });
    this.modal.set(true);
  }

  openCreateForStatus(status: TaskStatus): void {
    this.editingTask.set(null);
    this.taskForm.reset({
      title: '', description: '', status, priority: 'Medium', dueDate: '',
    });
    this.modal.set(true);
  }

  openEdit(task: TaskItem): void {
    this.editingTask.set(task);
    this.taskForm.setValue({
      title:       task.title,
      description: task.description,
      status:      task.status,
      priority:    task.priority,
      dueDate:     task.dueDate,
    });
    this.modal.set(true);
  }

  closeModal(): void {
    this.modal.set(false);
    this.editingTask.set(null);
  }

  saveTask(): void {
    if (this.taskForm.invalid) { this.taskForm.markAllAsTouched(); return; }
    this.saving.set(true);

    const raw = this.taskForm.getRawValue();
    const req: TaskRequest = {
      title:       raw.title,
      description: raw.description,
      status:      raw.status as TaskStatus,
      priority:    raw.priority as TaskPriority,
      dueDate:     raw.dueDate,
    };

    const editing = this.editingTask();
    const op$ = editing
      ? this.taskSvc.updateTask(editing.id, req)
      : this.taskSvc.createTask(req);

    op$.subscribe({
      next: saved => {
        if (editing) {
          this.tasks.update(list => list.map(t => t.id === saved.id ? saved : t));
          if (this.detailTask()?.id === saved.id) this.detailTask.set(saved);
        } else {
          this.tasks.update(list => [saved, ...list]);
        }
        this.toast.show(editing ? 'Task updated.' : 'Task created.', 'success');
        this.saving.set(false);
        this.closeModal();
      },
      error: () => {
        this.toast.show('Save failed.', 'error');
        this.saving.set(false);
      },
    });
  }

  // ── Delete ─────────────────────────────────────────────────────────────────
  deleteTask(task: TaskItem): void {
    if (!confirm(`Delete "${task.title}"?`)) return;
    this.deleting.set(true);
    this.taskSvc.deleteTask(task.id).subscribe({
      next: () => {
        this.tasks.update(list => list.filter(t => t.id !== task.id));
        if (this.detailTask()?.id === task.id) this.detailTask.set(null);
        this.toast.show('Task deleted.', 'success');
        this.deleting.set(false);
      },
      error: () => {
        this.toast.show('Delete failed.', 'error');
        this.deleting.set(false);
      },
    });
  }

  // ── Status change (from detail panel) ────────────────────────────────────
  changeStatus(task: TaskItem, status: TaskStatus): void {
    const req: TaskRequest = {
      title:       task.title,
      description: task.description,
      status,
      priority:    task.priority,
      dueDate:     task.dueDate,
    };
    this.taskSvc.updateTask(task.id, req).subscribe({
      next: saved => {
        this.tasks.update(list => list.map(t => t.id === saved.id ? saved : t));
        this.detailTask.set(saved);
        this.toast.show('Status updated.', 'success');
      },
      error: () => this.toast.show('Update failed.', 'error'),
    });
  }

  // ── Board (drag & drop) ───────────────────────────────────────────────────
  onDragStart(event: DragEvent, taskId: string): void {
    this.draggedId = taskId;
    event.dataTransfer?.setData('text/plain', taskId);
  }

  onDrop(event: DragEvent, status: TaskStatus): void {
    event.preventDefault();
    const id = this.draggedId ?? event.dataTransfer?.getData('text/plain');
    if (!id) return;
    this.draggedId = null;
    const task = this.tasks().find(t => t.id === id);
    if (!task || task.status === status) return;
    this.changeStatus(task, status);
  }

  loadMore(colKey: string): void {
    this.colShown.update(s => ({ ...s, [colKey]: (s[colKey] ?? 8) + 8 }));
  }

  // ── Pagination ─────────────────────────────────────────────────────────────
  onPerPageChange(event: Event): void {
    const val = parseInt((event.target as HTMLSelectElement).value, 10);
    this.perPage.set(val);
    this.page.set(1);
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  priorityMeta(p: TaskPriority) { return PRIORITY_META[p] ?? PRIORITY_META.Medium; }
  statusMeta(s: TaskStatus)     { return STATUS_META[s] ?? STATUS_META.Todo; }

  isOverdue(task: TaskItem): boolean {
    if (task.status === 'Done') return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return new Date(task.dueDate) < today;
  }

  dueDateDisplay(task: TaskItem): string {
    const formatted = this.datePipe.transform(task.dueDate, 'MMM d, yyyy') ?? task.dueDate;
    return this.isOverdue(task) ? `Overdue · ${formatted}` : formatted;
  }

  cycleSort(col: 'title' | 'priority' | 'status' | 'dueDate'): void {
    if (this.sortCol() !== col) {
      this.sortCol.set(col);
      this.sortDir.set('asc');
    } else if (this.sortDir() === 'asc') {
      this.sortDir.set('desc');
    } else {
      this.sortCol.set(null);
    }
  }

  sortIcon(col: string): string {
    if (this.sortCol() !== col) return '↕';
    return this.sortDir() === 'asc' ? '↑' : '↓';
  }

  formDirty(field: string): boolean {
    const c = this.taskForm.get(field);
    return !!(c?.invalid && (c.dirty || c.touched));
  }
}
