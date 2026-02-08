import { Component, computed, inject, signal, effect } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsersService, User } from '../../store/users.service';

@Component({
  selector: 'app-admin-users-page',
  imports: [NgIf, NgFor, FormsModule],
  templateUrl: './admin-users.page.html',
  styleUrl: './admin-users.page.css'
})
export class AdminUsersPage {
  private readonly userService = inject(UsersService);

  readonly searchQuery = signal('');
  readonly currentPage = signal(1);
  readonly pageSize = 10;
  
  readonly users = signal<User[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  readonly totalPages = computed(() => Math.ceil(this.total() / this.pageSize));
  
  readonly pages = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    let start = Math.max(current - 2, 1);
    let end = Math.min(start + 4, total);
    if (end === total) start = Math.max(end - 4, 1);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });

  readonly selectedUsers = signal<Set<number>>(new Set());
  readonly isAllSelected = computed(() => {
    const pageUsers = this.users();
    if (pageUsers.length === 0) return false;
    const selected = this.selectedUsers();
    return pageUsers.every(u => selected.has(u.id));
  });

  readonly hasSelection = computed(() => this.selectedUsers().size > 0);

  constructor() {
    effect(() => {
      this.loadUsers();
    });
  }

  loadUsers() {
    this.loading.set(true);
    this.userService.getUsers(this.currentPage(), this.pageSize, this.searchQuery())
      .subscribe({
        next: (res) => {
          this.users.set(res.items);
          this.total.set(res.total);
          this.loading.set(false);
          this.selectedUsers.set(new Set());
        },
        error: () => this.loading.set(false)
      });
  }

  onSearch(q: string) {
    this.searchQuery.set(q);
    this.currentPage.set(1);
  }

  setPage(p: number) {
    if (p >= 1 && p <= this.totalPages()) {
      this.currentPage.set(p);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  toggleSelection(id: number) {
    const selected = new Set(this.selectedUsers());
    if (selected.has(id)) selected.delete(id);
    else selected.add(id);
    this.selectedUsers.set(selected);
  }

  toggleAll() {
    const pageUsers = this.users();
    const selected = new Set(this.selectedUsers());
    const allSelected = this.isAllSelected();
    
    if (allSelected) {
      pageUsers.forEach(u => selected.delete(u.id));
    } else {
      pageUsers.forEach(u => selected.add(u.id));
    }
    this.selectedUsers.set(selected);
  }

  deleteUser(id: number) {
    if (!confirm('Are you sure you want to delete this user?')) return;
    this.userService.deleteUser(id).subscribe(() => this.loadUsers());
  }

  deleteSelected() {
    const ids = Array.from(this.selectedUsers());
    if (ids.length === 0 || !confirm(`Delete ${ids.length} users?`)) return;
    
    this.userService.deleteUsers(ids).subscribe(() => {
      this.loadUsers();
    });
  }
}
