/**
 * School Management System — Global Application JavaScript
 * Manages API client, authentication tokens, theme toggling, and UI feedback
 */

const App = (function () {
  // Configuration
  const API_BASE_URL = window.API_BASE_URL || '';
  const TOKEN_KEY = 'sms_auth_token';
  const USER_KEY = 'sms_auth_user';
  const THEME_KEY = 'sms_theme';

  // State
  let currentToken = localStorage.getItem(TOKEN_KEY) || sessionStorage.getItem(TOKEN_KEY) || null;
  let currentUser = null;

  try {
    const storedUser = localStorage.getItem(USER_KEY) || sessionStorage.getItem(USER_KEY);
    if (storedUser) currentUser = JSON.parse(storedUser);
  } catch (e) {
    currentUser = null;
  }

  // Initialization
  function init() {
    initTheme();
    initSidebar();
    updateUserInterface();
  }

  // Theme Management
  function initTheme() {
    const savedTheme = localStorage.getItem(THEME_KEY) || 'light';
    setTheme(savedTheme);

    const themeToggleBtn = document.getElementById('theme-toggle-btn');
    if (themeToggleBtn) {
      themeToggleBtn.addEventListener('click', toggleTheme);
    }
  }

  function setTheme(theme) {
    document.documentElement.setAttribute('data-bs-theme', theme);
    localStorage.setItem(THEME_KEY, theme);

    const icon = document.getElementById('theme-icon');
    if (icon) {
      icon.className = theme === 'dark' ? 'bi bi-sun-fill' : 'bi bi-moon-stars-fill';
    }
  }

  function toggleTheme() {
    const current = document.documentElement.getAttribute('data-bs-theme') || 'light';
    setTheme(current === 'dark' ? 'light' : 'dark');
  }

  // Sidebar Management
  function initSidebar() {
    const sidebarToggle = document.getElementById('sidebar-toggle');
    const sidebar = document.getElementById('app-sidebar');
    const overlay = document.getElementById('sidebar-overlay');

    if (sidebarToggle && sidebar) {
      sidebarToggle.addEventListener('click', () => {
        sidebar.classList.toggle('show');
        if (overlay) overlay.classList.toggle('show');
      });
    }

    if (overlay && sidebar) {
      overlay.addEventListener('click', () => {
        sidebar.classList.remove('show');
        overlay.classList.remove('show');
      });
    }
  }

  // Auth & Session Management
  function setSession(token, user, rememberMe = true) {
    currentToken = token;
    currentUser = user;
    const storage = rememberMe ? localStorage : sessionStorage;

    storage.setItem(TOKEN_KEY, token);
    storage.setItem(USER_KEY, JSON.stringify(user));
    
    // Also set a session cookie for server-side layout rendering
    document.cookie = `sms_user_name=${encodeURIComponent(user.fullName || '')}; path=/; max-age=86400`;
    document.cookie = `sms_user_role=${encodeURIComponent(user.roleType || '')}; path=/; max-age=86400`;
    document.cookie = `sms_user_org=${encodeURIComponent(user.organizationName || 'Master Platform')}; path=/; max-age=86400`;
    document.cookie = `sms_user_branch=${encodeURIComponent(user.branchName || 'Main Campus')}; path=/; max-age=86400`;

    updateUserInterface();
  }

  function clearSession() {
    currentToken = null;
    currentUser = null;
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    document.cookie = 'sms_user_name=; path=/; max-age=0';
    document.cookie = 'sms_user_role=; path=/; max-age=0';
    document.cookie = 'sms_user_org=; path=/; max-age=0';
    document.cookie = 'sms_user_branch=; path=/; max-age=0';
  }

  function logout() {
    clearSession();
    window.location.href = '/login';
  }

  // Role metadata & helpers
  const RoleNames = {
    1: 'SuperAdmin',
    2: 'OrganizationAdmin',
    3: 'Principal',
    4: 'Teacher',
    5: 'Accountant',
    6: 'Staff',
    7: 'Student',
    8: 'Parent'
  };

  const RoleLabels = {
    'SuperAdmin': 'Platform Super Admin',
    'OrganizationAdmin': 'Institution Administrator',
    'Principal': 'School Principal',
    'Teacher': 'Faculty / Teacher',
    'Accountant': 'School Accountant',
    'Staff': 'Campus Staff',
    'Student': 'Student Portal',
    'Parent': 'Parent Portal'
  };

  const RoleBadges = {
    'SuperAdmin': { bg: 'bg-warning text-dark', icon: 'bi-shield-shaded' },
    'OrganizationAdmin': { bg: 'bg-primary text-white', icon: 'bi-building' },
    'Principal': { bg: 'bg-primary text-white', icon: 'bi-award' },
    'Teacher': { bg: 'bg-info text-dark', icon: 'bi-person-video3' },
    'Accountant': { bg: 'bg-success text-white', icon: 'bi-cash-coin' },
    'Staff': { bg: 'bg-secondary text-white', icon: 'bi-person-badge' },
    'Student': { bg: 'bg-primary text-white', icon: 'bi-mortarboard' },
    'Parent': { bg: 'bg-dark text-white', icon: 'bi-people' }
  };

  function getRoleKey(roleType) {
    if (typeof roleType === 'number') return RoleNames[roleType] || 'User';
    if (!isNaN(Number(roleType))) return RoleNames[Number(roleType)] || 'User';
    return String(roleType);
  }

  function hasRole(...roles) {
    if (!currentUser) return false;
    const currentKey = getRoleKey(currentUser.roleType).toLowerCase();
    return roles.some(r => {
      const target = (typeof r === 'number' ? (RoleNames[r] || '') : r).toLowerCase();
      if (target === 'orgadmin' && (currentKey === 'organizationadmin' || currentKey === 'principal')) return true;
      if (target === 'organizationadmin' && (currentKey === 'organizationadmin' || currentKey === 'principal')) return true;
      return currentKey === target;
    });
  }

  function hasPermission(permissionCode) {
    if (!currentUser) return false;
    if (hasRole('SuperAdmin')) return true;
    const perms = currentUser.permissions || [];
    return perms.includes(permissionCode);
  }

  function updateUserInterface() {
    if (!currentUser) {
      document.querySelectorAll('[data-roles], [data-permission], .js-superadmin-only, .js-orgadmin-only').forEach(el => {
        el.style.setProperty('display', 'none', 'important');
      });
      if (window.location.pathname.toLowerCase() !== '/login') {
        window.location.replace('/login');
      }
      return;
    }

    const roleKey = getRoleKey(currentUser.roleType);
    const roleLabel = RoleLabels[roleKey] || roleKey;
    const badgeInfo = RoleBadges[roleKey] || { bg: 'bg-secondary text-white', icon: 'bi-person' };

    // Update text elements
    document.querySelectorAll('.js-user-name').forEach(el => el.textContent = currentUser.fullName || currentUser.email);
    document.querySelectorAll('.js-user-email').forEach(el => el.textContent = currentUser.email || '');
    document.querySelectorAll('.js-user-role').forEach(el => el.textContent = roleLabel);
    document.querySelectorAll('.js-org-name').forEach(el => el.textContent = currentUser.organizationName || 'Master Platform');
    document.querySelectorAll('.js-branch-name').forEach(el => el.textContent = currentUser.branchName || 'Main Campus');

    // Role badge in Topbar
    const roleBadgeEl = document.getElementById('topbar-role-badge');
    if (roleBadgeEl) {
      roleBadgeEl.className = `badge ${badgeInfo.bg} px-3 py-2 rounded-pill fw-semibold shadow-sm d-flex align-items-center gap-1`;
      roleBadgeEl.innerHTML = `<i class="bi ${badgeInfo.icon}"></i> <span>${roleLabel}</span>`;
    }

    // Dynamic Organization Branding & Logo
    const sidebarLogo = document.getElementById('sidebar-org-logo');
    const sidebarDefaultIcon = document.getElementById('sidebar-default-icon');
    const sidebarBrandText = document.getElementById('sidebar-brand-text');
    const topbarOrgLogo = document.getElementById('topbar-org-logo');
    const topbarOrgIcon = document.getElementById('topbar-org-icon');

    const isSuperAdmin = hasRole('SuperAdmin');
    const customLogoUrl = (!isSuperAdmin && currentUser.organizationLogoUrl) ? currentUser.organizationLogoUrl : null;

    if (customLogoUrl) {
      if (sidebarLogo) {
        sidebarLogo.src = customLogoUrl;
        sidebarLogo.classList.remove('d-none');
      }
      if (sidebarDefaultIcon) sidebarDefaultIcon.classList.add('d-none');
      if (sidebarBrandText && currentUser.organizationName) {
        sidebarBrandText.innerHTML = `<span class="fw-bold text-truncate d-inline-block" style="max-width: 140px;" title="${currentUser.organizationName}">${currentUser.organizationName}</span>`;
      }
      if (topbarOrgLogo) {
        topbarOrgLogo.src = customLogoUrl;
        topbarOrgLogo.classList.remove('d-none');
      }
      if (topbarOrgIcon) topbarOrgIcon.classList.add('d-none');
    } else {
      if (sidebarLogo) sidebarLogo.classList.add('d-none');
      if (sidebarDefaultIcon) sidebarDefaultIcon.classList.remove('d-none');
      if (sidebarBrandText) sidebarBrandText.innerHTML = `School<span>Sync</span>`;
      if (topbarOrgLogo) topbarOrgLogo.classList.add('d-none');
      if (topbarOrgIcon) topbarOrgIcon.classList.remove('d-none');
    }

    // Helper to determine display style
    function getDisplayStyle(el) {
      if (el.classList.contains('sidebar-item') || el.classList.contains('btn') || el.classList.contains('d-flex')) return 'flex';
      if (el.tagName === 'TR') return 'table-row';
      if (el.tagName === 'TD' || el.tagName === 'TH') return 'table-cell';
      return 'block';
    }

    // 1. Dynamic Role-based visibility using [data-roles="SuperAdmin,Teacher"]
    document.querySelectorAll('[data-roles]').forEach(el => {
      const allowedRoles = el.getAttribute('data-roles').split(',').map(r => r.trim().toLowerCase());
      const current = roleKey.toLowerCase();
      const match = allowedRoles.some(r => {
        if (r === current) return true;
        if ((r === 'orgadmin' || r === 'organizationadmin') && (current === 'organizationadmin' || current === 'principal')) return true;
        if (r === 'superadmin' && current === 'superadmin') return true;
        return false;
      });
      
      if (match) {
        el.style.setProperty('display', getDisplayStyle(el), 'important');
      } else {
        el.style.setProperty('display', 'none', 'important');
      }
    });

    // 2. Dynamic Permission-based visibility using [data-permission="Students.View"]
    document.querySelectorAll('[data-permission]').forEach(el => {
      const perm = el.getAttribute('data-permission');
      if (hasPermission(perm)) {
        el.style.setProperty('display', getDisplayStyle(el), 'important');
      } else {
        el.style.setProperty('display', 'none', 'important');
      }
    });

    // 3. Backwards-compatible js-superadmin-only
    document.querySelectorAll('.js-superadmin-only').forEach(el => {
      if (hasRole('SuperAdmin')) {
        el.style.setProperty('display', getDisplayStyle(el), 'important');
      } else {
        el.style.setProperty('display', 'none', 'important');
      }
    });

    // 4. Backwards-compatible js-orgadmin-only
    document.querySelectorAll('.js-orgadmin-only').forEach(el => {
      if (hasRole('OrganizationAdmin', 'Principal')) {
        el.style.setProperty('display', getDisplayStyle(el), 'important');
      } else {
        el.style.setProperty('display', 'none', 'important');
      }
    });

    // 5. Dynamic Module-based licensing visibility using [data-module="Library"]
    document.querySelectorAll('[data-module]').forEach(el => {
      const targetModule = el.getAttribute('data-module');
      if (!targetModule) return;

      // SuperAdmin is platform company and only manages organizations, not school-internal operations
      if (isSuperAdmin) {
        if (!['organizations', 'audit'].includes(targetModule.toLowerCase())) {
          el.style.setProperty('display', 'none', 'important');
          return;
        }
      }

      // If user has enabledModuleNames configured from server (e.g. OrgAdmin, Teacher, Staff)
      if (currentUser.enabledModuleNames && Array.isArray(currentUser.enabledModuleNames)) {
        const isModuleEnabled = currentUser.enabledModuleNames.some(m => m.toLowerCase() === targetModule.toLowerCase());
        if (!isModuleEnabled) {
          el.style.setProperty('display', 'none', 'important');
        }
      }
    });
  }

  // Centralized API Request Wrapper
  async function apiFetch(endpoint, options = {}) {
    let url;
    if (endpoint.startsWith('http')) {
      url = endpoint;
    } else if (API_BASE_URL) {
      url = `${API_BASE_URL}${endpoint.startsWith('/') ? '' : '/'}${endpoint}`;
    } else {
      url = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
    }

    const headers = {
      'Accept': 'application/json',
      'Content-Type': 'application/json',
      ...(options.headers || {})
    };

    if (currentToken) {
      headers['Authorization'] = `Bearer ${currentToken}`;
    }

    try {
      const response = await fetch(url, { ...options, headers });

      if (response.status === 401) {
        showToast('error', 'Session Expired', 'Please log in again.');
        setTimeout(() => logout(), 1200);
        return { success: false, message: 'Unauthorized' };
      }

      if (response.status === 403) {
        showToast('error', 'Access Denied', 'You do not have permission for this action.');
        return { success: false, message: 'Forbidden' };
      }

      if (response.status === 503) {
        const errorData = await response.json().catch(() => ({}));
        const msg = errorData.message || 'The SchoolManagement.Api backend is offline. Please start SchoolManagement.Api.';
        showToast('error', 'API Offline', msg);
        return { success: false, message: msg };
      }

      const json = await response.json();
      const payload = (json && json.data !== undefined) ? json.data : json;
      const proxyData = (payload && typeof payload === 'object') ? new Proxy(payload, {
        get(target, prop) {
          if (prop === 'data') return target;
          if (prop === 'success') return json.success;
          if (prop === 'message') return json.message;
          return target[prop];
        }
      }) : payload;

      return {
        ok: response.ok,
        status: response.status,
        success: json ? (json.success ?? response.ok) : response.ok,
        message: json ? json.message : null,
        data: proxyData,
        raw: json
      };
    } catch (err) {
      console.error('API Network Error:', err);
      showToast('error', 'Network Error', 'Could not reach server. Please ensure the backend is running.');
      return { ok: false, success: false, message: err.message, data: null };
    }
  }

  // Toast Notifications
  function showToast(type, title, message) {
    let container = document.getElementById('toast-container');
    if (!container) {
      container = document.createElement('div');
      container.id = 'toast-container';
      document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = 'saas-toast';

    let iconClass = 'bi-info-circle text-info';
    let borderColor = 'var(--info)';

    if (type === 'success') {
      iconClass = 'bi-check-circle-fill text-success';
      borderColor = 'var(--success)';
    } else if (type === 'error') {
      iconClass = 'bi-x-circle-fill text-danger';
      borderColor = 'var(--danger)';
    } else if (type === 'warning') {
      iconClass = 'bi-exclamation-triangle-fill text-warning';
      borderColor = 'var(--warning)';
    }

    toast.style.borderLeft = `4px solid ${borderColor}`;
    toast.innerHTML = `
      <i class="bi ${iconClass} fs-5 mt-1"></i>
      <div class="flex-grow-1">
        <div class="fw-bold fs-6 mb-1">${title}</div>
        <div class="text-secondary small">${message}</div>
      </div>
      <button type="button" class="btn-close ms-2" onclick="this.parentElement.remove()"></button>
    `;

    container.appendChild(toast);

    setTimeout(() => {
      if (toast.parentElement) toast.remove();
    }, 4500);
  }

  // Public API
  return {
    init,
    apiFetch,
    setSession,
    clearSession,
    logout,
    showToast,
    hasRole,
    hasPermission,
    getRoleKey: () => (currentUser ? getRoleKey(currentUser.roleType) : 'Guest'),
    getRoleLabel: () => (currentUser ? (RoleLabels[getRoleKey(currentUser.roleType)] || 'User') : 'Guest'),
    getToken: () => currentToken,
    getUser: () => currentUser,
    isAuthenticated: () => !!currentToken
  };
})();

// Global helpers for all Razor views
window.apiFetch = (endpoint, options) => App.apiFetch(endpoint, options);
window.showToast = (param1, param2, param3) => {
  if (param3 !== undefined) {
    App.showToast(param1, param2, param3);
  } else if (param2 !== undefined) {
    const type = (param2 === 'danger' || param2 === 'error') ? 'error' : (param2 === 'success' ? 'success' : 'info');
    App.showToast(type, type === 'error' ? 'Notice' : 'Success', param1);
  } else {
    App.showToast('info', 'Notification', param1);
  }
};

document.addEventListener('DOMContentLoaded', () => {
  App.init();
});

