export function initialize(dbName, version, stores) {
    const promise = new Promise((resolve, reject) => {
        let db;
        const request = indexedDB.open(dbName, version);
        request.onupgradeneeded = event => {
            db = event.target.result;
            stores.forEach(store => {
                if (db.objectStoreNames.contains(store.name)) {
                    db.deleteObjectStore(store.name);
                }
                let objectStore = db.createObjectStore(store.name, { keyPath: store.keyPath });
                store.indexes?.forEach(index => {
                    objectStore.createIndex(index.name, index.keyPath, {
                        unique: index.unique,
                        multiEntry: index.multiEntry
                    });
                });
            });
        };
        request.onsuccess = async event => {
            db = event.target.result;
            resolve(db);
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown initialization error');
        };
    });

    return promise;
}

export function add(db, storeName, value) {
    const promise = new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readwrite");

        const objectStore = transaction.objectStore(storeName);
        const request = objectStore.add(value);
        request.onsuccess = async event => {
            resolve(event.target.result);
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown add error');
        };
    });

    return promise;
}

export function deleteRecord(db, storeName, key) {
    const promise = new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readwrite");
        const objectStore = transaction.objectStore(storeName);
        const request = objectStore.delete(key);
        request.onsuccess = async event => {
            resolve();
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown delete error');
        };
    });

    return promise;
}

export function get(db, storeName, key) {
    const promise = new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readonly");

        const objectStore = transaction.objectStore(storeName);
        const request = objectStore.get(key);
        request.onsuccess = async event => {
            resolve(event.target.result);
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown get error');
        };
    });

    return promise;
}

export function put(db, storeName, value) {
    const promise = new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readwrite");

        const objectStore = transaction.objectStore(storeName);
        const request = objectStore.put(value);
        request.onsuccess = async event => {
            resolve();
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown put error');
        };
    });

    return promise;
}

export function getAll(db, storeName) {
    const promise = new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readonly");

        const objectStore = transaction.objectStore(storeName);
        const request = objectStore.getAll();
        request.onsuccess = async event => {
            resolve(event.target.result);
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown getAll error');
        };
    });

    return promise;
}

export function clearStore(db, storeName) {
    const promise = new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readwrite");

        const objectStore = transaction.objectStore(storeName);
        const request = objectStore.clear();
        request.onsuccess = async event => {
            resolve();
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown clear error');
        };
    });

    return promise;
}

export function count(db, storeName) {
    const promise = new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readonly");

        const objectStore = transaction.objectStore(storeName);
        const request = objectStore.count();
        request.onsuccess = async event => {
            resolve(event.target.result);
        };
        request.onerror = async event => {
            reject(event.target.error?.toString() || 'Unknown count error');
        };
    });

    return promise;
}

export function closeDatabase(db) {
    if (db && typeof db.close === 'function') {
        db.close();
    }
}