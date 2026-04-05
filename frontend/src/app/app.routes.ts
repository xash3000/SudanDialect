import { Routes } from '@angular/router';
import { HomePageComponent } from './pages/home-page/home-page.component';

export const routes: Routes = [
  {
    path: '',
    component: HomePageComponent
  },
  {
    path: 'word/:id',
    component: HomePageComponent
  },
  {
    path: '**',
    redirectTo: ''
  }
];
