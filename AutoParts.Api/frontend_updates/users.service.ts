import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type UserRole = 'customer' | 'admin';

export type User = {
  id: number;
  role: string;
  phone: string;
  fullName: string;
  email: string;
  address: string;
  location?: string;
};

export type UserResponse = {
  items: User[];
  total: number;
  page: number;
  size: number;
};

const API_BASE_URL = 'http://localhost:5221/api/user';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  getUsers(page: number, size: number, search: string = ''): Observable<UserResponse> {
    let params: any = { page, size };
    if (search) params.search = search;
    return this.http.get<UserResponse>(`${API_BASE_URL}/list`, { params });
  }

  deleteUser(id: number): Observable<any> {
    return this.http.delete(`${API_BASE_URL}/${id}`);
  }

  deleteUsers(ids: number[]): Observable<any> {
    return this.http.post(`${API_BASE_URL}/delete-bulk`, ids);
  }
}
