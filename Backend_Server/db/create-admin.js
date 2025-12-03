/**
 * 어드민 계정 생성 스크립트
 * 사용법: node db/create-admin.js [admin_id] [admin_password]
 * 예시: node db/create-admin.js admin Admin123!@#
 */

const bcrypt = require('bcrypt');
const { getPool } = require('./mysql/connectMySQL');
const { CONFIG } = require('../constants');

async function createAdmin() {
  // 명령줄 인자에서 ID와 비밀번호 가져오기
  const adminId = process.argv[2] || 'admin';
  const adminPassword = process.argv[3] || 'Admin123!@#';

  // 비밀번호 검증 (최소 10자, 대소문자, 숫자, 기호 포함)
  const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{10,}$/;
  
  if (!passwordRegex.test(adminPassword)) {
    console.error('❌ 비밀번호는 최소 10자 이상이며, 대문자, 소문자, 숫자, 특수문자를 포함해야 합니다.');
    console.error('예시: Admin123!@#');
    process.exit(1);
  }

  try {
    // MySQL 연결
    const pool = getPool();
    console.log('✓ MySQL 연결 성공');

    // 기존 어드민 계정 확인
    const [existingUsers] = await pool.execute(
      'SELECT index_id, ID, role FROM User WHERE ID = ?',
      [adminId]
    );

    if (existingUsers.length > 0) {
      const existingUser = existingUsers[0];
      if (existingUser.role === CONFIG.ROLES.ADMIN) {
        console.log(`⚠️  어드민 계정 "${adminId}"이 이미 존재합니다.`);
        console.log(`   기존 계정의 비밀번호를 업데이트합니다...`);
        // 간단하게 업데이트 진행
        const passwordHash = await bcrypt.hash(adminPassword, 10);
        await pool.execute(
          'UPDATE User SET PW = ?, role = ? WHERE ID = ?',
          [passwordHash, CONFIG.ROLES.ADMIN, adminId]
        );
        console.log(`✓ 어드민 계정 "${adminId}"의 비밀번호가 업데이트되었습니다.`);
        process.exit(0);
      } else {
        // role을 admin으로 변경
        const passwordHash = await bcrypt.hash(adminPassword, 10);
        await pool.execute(
          'UPDATE User SET PW = ?, role = ? WHERE ID = ?',
          [passwordHash, CONFIG.ROLES.ADMIN, adminId]
        );
        console.log(`✓ 사용자 "${adminId}"의 role이 admin으로 변경되었습니다.`);
        process.exit(0);
      }
    }

    // 비밀번호 해시화
    console.log('비밀번호 해시화 중...');
    const passwordHash = await bcrypt.hash(adminPassword, 10);

    // 어드민 계정 생성
    const [result] = await pool.execute(
      'INSERT INTO User (ID, PW, role, total_points) VALUES (?, ?, ?, ?)',
      [adminId, passwordHash, CONFIG.ROLES.ADMIN, 0]
    );

    console.log('\n✅ 어드민 계정이 성공적으로 생성되었습니다!');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log(`ID: ${adminId}`);
    console.log(`Password: ${adminPassword}`);
    console.log(`Role: ${CONFIG.ROLES.ADMIN}`);
    console.log(`Index ID: ${result.insertId}`);
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('\n⚠️  보안을 위해 비밀번호를 안전한 곳에 저장하세요!');

    process.exit(0);
  } catch (error) {
    console.error('❌ 어드민 계정 생성 실패:', error.message);
    console.error(error);
    process.exit(1);
  }
}

// MySQL 연결 후 스크립트 실행
const { connectMySQL } = require('./mysql/connectMySQL');

connectMySQL()
  .then(() => {
    console.log('어드민 계정 생성 스크립트 시작...\n');
    createAdmin();
  })
  .catch((error) => {
    console.error('❌ MySQL 연결 실패:', error.message);
    process.exit(1);
  });

